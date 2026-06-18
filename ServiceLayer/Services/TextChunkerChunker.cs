#pragma warning disable SKEXP0050
using Microsoft.SemanticKernel.Text;

namespace ServiceLayer.Services;

/// <summary>
/// Chunking bằng Microsoft Semantic Kernel <see cref="TextChunker"/> — giống Assignment_1.
/// Đo độ dài theo KÝ TỰ: tách văn bản thành dòng ≤200 ký tự, rồi gom thành đoạn (chunk)
/// ≤800 ký tự với overlap 100 ký tự; bỏ các chunk ngắn hơn 50 ký tự.
/// </summary>
public class TextChunkerChunker : IChunker
{
    // Theo spec: 500–1000 ký tự mỗi chunk.
    private const int LineMaxChars = 200;
    private const int ChunkSize = 800;
    private const int ChunkOverlap = 100;
    private const int MinChunkLength = 50;

    public List<(string Text, int Page)> Chunk(List<(int Page, string Text)> pages)
    {
        var chunks = new List<(string Text, int Page)>();

        // TokenCounter tùy biến: tính độ dài theo số ký tự (Characters) thay vì token.
        TextChunker.TokenCounter characterCounter = input => input.Length;

        foreach (var (page, text) in pages)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;

            // 1) Tách trang thành các dòng/câu nhỏ (≤200 ký tự) để không cắt giữa câu tùy tiện.
            var lines = TextChunker.SplitPlainTextLines(text, LineMaxChars, characterCounter);

            // 2) Gom các dòng thành đoạn lớn (≤800 ký tự) với overlap 100 ký tự.
            var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, ChunkSize, ChunkOverlap, tokenCounter: characterCounter);

            foreach (var p in paragraphs)
            {
                var trimmed = p.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Length >= MinChunkLength)
                    chunks.Add((trimmed, page));
            }
        }

        return chunks;
    }
}
