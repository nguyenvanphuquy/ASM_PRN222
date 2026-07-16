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
                var page = GuessPage(text, parts);
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

    private static int GuessPage(string chunk, List<(int Page, string Text)> parts)
    {
        if (parts == null || parts.Count == 0) return 1;

        var probe = chunk.Length <= 80 ? chunk : chunk[..80];
        var cleanProbe = Regex.Replace(probe, @"\s+", " ").Trim();
        if (string.IsNullOrEmpty(cleanProbe)) return parts[0].Page;

        // 1. Try to find an exact or whitespace-normalized match in a single page
        foreach (var (page, text) in parts)
        {
            var cleanText = Regex.Replace(text, @"\s+", " ");
            if (cleanText.Contains(cleanProbe, StringComparison.OrdinalIgnoreCase))
                return page;
        }

        // 2. Map match index in combined text to page
        var combinedText = string.Join("\n\n", parts.Select(p => p.Text));
        var cleanCombined = Regex.Replace(combinedText, @"\s+", " ");
        var matchIndex = cleanCombined.IndexOf(cleanProbe, StringComparison.OrdinalIgnoreCase);
        if (matchIndex >= 0)
        {
            int currentLength = 0;
            foreach (var (page, text) in parts)
            {
                var cleanText = Regex.Replace(text, @"\s+", " ");
                currentLength += cleanText.Length + 1; // +1 for space replacing "\n\n"
                if (matchIndex < currentLength)
                    return page;
            }
        }

        // 3. Fallback to shorter probe
        if (cleanProbe.Length > 20)
        {
            var shortProbe = cleanProbe[..20];
            foreach (var (page, text) in parts)
            {
                var cleanText = Regex.Replace(text, @"\s+", " ");
                if (cleanText.Contains(shortProbe, StringComparison.OrdinalIgnoreCase))
                    return page;
            }
        }

        return parts[0].Page;
    }
}
