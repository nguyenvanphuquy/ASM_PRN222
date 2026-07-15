using ServiceLayer.Services.Interfaces;
using System.Text.RegularExpressions;

namespace ServiceLayer.Services.Implementations;

public class SentenceChunkingStrategy : IChunkingStrategy
{
    public string Name => "Sentence";

    private const int MinAlphaNum = 40;
    private const int TargetMerge = 600;

    public List<(string Text, int Page)> Chunk(List<(int Page, string Text)> pages)
    {
        var parts = pages
            .Select(p => (p.Page, Text: TextExtractor.NormalizeText(p.Text)))
            .Where(p => !string.IsNullOrWhiteSpace(p.Text))
            .ToList();
        if (parts.Count == 0) return [];

        var combined = string.Join("\n\n", parts.Select(p => p.Text));
        var sentences = Regex.Split(combined, @"(?<=[\.!\?])\s+")
            .Select(TextExtractor.NormalizeText)
            .Where(s => TextExtractor.CountAlphaNum(s) >= 12)
            .ToList();

        // Gom câu ngắn thành đoạn ~TargetMerge ký tự.
        var chunks = new List<(string Text, int Page)>();
        var buf = new List<string>();
        var bufLen = 0;

        void Flush()
        {
            if (buf.Count == 0) return;
            var text = TextExtractor.NormalizeText(string.Join(" ", buf));
            if (TextExtractor.CountAlphaNum(text) >= MinAlphaNum)
            {
                var probe = text.Length <= 60 ? text : text[..60];
                var page = parts.FirstOrDefault(p => p.Text.Contains(probe, StringComparison.Ordinal)).Page;
                if (page == 0) page = parts[0].Page;
                chunks.Add((text, page));
            }
            buf.Clear();
            bufLen = 0;
        }

        foreach (var s in sentences)
        {
            buf.Add(s);
            bufLen += s.Length;
            if (bufLen >= TargetMerge) Flush();
        }
        Flush();
        return chunks;
    }
}
