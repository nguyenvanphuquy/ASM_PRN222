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
                var page = parts.FirstOrDefault(p => p.Text.Contains(
                    chunkText.Length <= 60 ? chunkText : chunkText[..60],
                    StringComparison.Ordinal)).Page;
                if (page == 0) page = parts[0].Page;
                chunks.Add((chunkText, page));
            }

            if (i + len >= combined.Length) break;
            i += ChunkSize - Overlap;
        }

        return chunks;
    }
}
