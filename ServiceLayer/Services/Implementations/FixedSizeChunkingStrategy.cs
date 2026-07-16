using ServiceLayer.Services.Interfaces;

namespace ServiceLayer.Services.Implementations;

public class FixedSizeChunkingStrategy : IChunkingStrategy
{
    public string Name => "FixedSize";

    private const int ChunkSize = 800;
    private const int Overlap = 100;
    private const int MinAlphaNum = 80;

    public List<(string Text, int Page)> Chunk(List<(int Page, string Text)> pages)
    {
        var parts = pages
            .Select(p => (p.Page, Text: TextExtractor.NormalizeText(p.Text)))
            .Where(p => !string.IsNullOrWhiteSpace(p.Text))
            .ToList();
        if (parts.Count == 0) return [];

        var combined = string.Join("\n\n", parts.Select(p => p.Text));
        var chunks = new List<(string Text, int Page)>();

        for (int i = 0; i < combined.Length; )
        {
            int len = Math.Min(ChunkSize, combined.Length - i);
            var chunkText = TextExtractor.NormalizeText(combined.Substring(i, len));
            if (TextExtractor.CountAlphaNum(chunkText) >= MinAlphaNum)
            {
                var page = GuessPage(chunkText, parts);
                chunks.Add((chunkText, page));
            }

            if (i + len >= combined.Length) break;
            i += ChunkSize - Overlap;
        }

        return chunks;
    }

    private static int GuessPage(string chunk, List<(int Page, string Text)> parts)
    {
        if (parts == null || parts.Count == 0) return 1;

        var probe = chunk.Length <= 80 ? chunk : chunk[..80];
        var cleanProbe = System.Text.RegularExpressions.Regex.Replace(probe, @"\s+", " ").Trim();
        if (string.IsNullOrEmpty(cleanProbe)) return parts[0].Page;

        // 1. Try to find an exact or whitespace-normalized match in a single page
        foreach (var (page, text) in parts)
        {
            var cleanText = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            if (cleanText.Contains(cleanProbe, StringComparison.OrdinalIgnoreCase))
                return page;
        }

        // 2. Map match index in combined text to page
        var combinedText = string.Join("\n\n", parts.Select(p => p.Text));
        var cleanCombined = System.Text.RegularExpressions.Regex.Replace(combinedText, @"\s+", " ");
        var matchIndex = cleanCombined.IndexOf(cleanProbe, StringComparison.OrdinalIgnoreCase);
        if (matchIndex >= 0)
        {
            int currentLength = 0;
            foreach (var (page, text) in parts)
            {
                var cleanText = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
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
                var cleanText = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
                if (cleanText.Contains(shortProbe, StringComparison.OrdinalIgnoreCase))
                    return page;
            }
        }

        return parts[0].Page;
    }
}
