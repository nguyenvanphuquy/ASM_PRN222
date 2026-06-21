namespace ServiceLayer.Services;

public class FixedSizeChunkingStrategy : IChunkingStrategy
{
    public string Name => "FixedSize";
    
    private const int ChunkSize = 500;
    private const int Overlap = 50;

    public List<(string Text, int Page)> Chunk(List<(int Page, string Text)> pages)
    {
        var chunks = new List<(string Text, int Page)>();

        foreach (var (page, text) in pages)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;

            int i = 0;
            while (i < text.Length)
            {
                int len = Math.Min(ChunkSize, text.Length - i);
                var chunkText = text.Substring(i, len).Trim();
                
                if (chunkText.Length >= 50)
                {
                    chunks.Add((chunkText, page));
                }

                i += ChunkSize - Overlap;
            }
        }

        return chunks;
    }
}
