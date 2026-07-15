#pragma warning disable SKEXP0050
using Microsoft.SemanticKernel.Text;
using ServiceLayer.Services.Interfaces;

namespace ServiceLayer.Services.Implementations;

/// <summary>
/// Chunking bằng Semantic Kernel TextChunker — đo độ dài theo ký tự.
/// Nhận text đã ghép (thường là cả tài liệu); bỏ chunk chỉ chứa khoảng trắng / mục lục ngắn.
/// </summary>
public class SemanticKernelStrategy : IChunkingStrategy
{
    public string Name => "SemanticKernel";

    private const int LineMaxChars = 200;
    private const int ChunkSize = 800;
    private const int ChunkOverlap = 100;
    private const int MinAlphaNum = 80; // đủ chữ thật, không chỉ tiêu đề TOC

    public List<(string Text, int Page)> Chunk(List<(int Page, string Text)> pages)
    {
        // Ghép mọi trang thành 1 stream — tránh chunk #0..#N chỉ là mục lục trang 1.
        var parts = pages
            .Select(p => (p.Page, Text: TextExtractor.NormalizeText(p.Text)))
            .Where(p => !string.IsNullOrWhiteSpace(p.Text))
            .ToList();
        if (parts.Count == 0) return [];

        var combined = string.Join("\n\n", parts.Select(p => p.Text));
        TextChunker.TokenCounter counter = s => s.Length;

        var lines = TextChunker.SplitPlainTextLines(combined, LineMaxChars, counter);
        var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, ChunkSize, ChunkOverlap, tokenCounter: counter);

        var result = new List<(string Text, int Page)>();
        foreach (var p in paragraphs)
        {
            var text = TextExtractor.NormalizeText(p);
            if (TextExtractor.CountAlphaNum(text) < MinAlphaNum) continue;
            result.Add((text, GuessPage(text, parts)));
        }

        return result.Count > 0 ? result : FallbackFixedWindows(combined, parts);
    }

    /// <summary>Nếu SK lọc hết (PDF lạ) → cắt cửa sổ cố định trên text đã chuẩn hoá.</summary>
    private static List<(string Text, int Page)> FallbackFixedWindows(
        string combined, List<(int Page, string Text)> parts)
    {
        const int size = 800, overlap = 100;
        var list = new List<(string, int)>();
        for (int i = 0; i < combined.Length; )
        {
            var len = Math.Min(size, combined.Length - i);
            var slice = TextExtractor.NormalizeText(combined.Substring(i, len));
            if (TextExtractor.CountAlphaNum(slice) >= MinAlphaNum)
                list.Add((slice, GuessPage(slice, parts)));
            if (i + len >= combined.Length) break;
            i += size - overlap;
        }
        return list;
    }

    private static int GuessPage(string chunk, List<(int Page, string Text)> parts)
    {
        // Trang có đoạn prefix của chunk trùng nhiều nhất.
        var probe = chunk.Length <= 80 ? chunk : chunk[..80];
        foreach (var (page, text) in parts)
        {
            if (text.Contains(probe, StringComparison.Ordinal))
                return page;
        }
        return parts[0].Page;
    }
}
