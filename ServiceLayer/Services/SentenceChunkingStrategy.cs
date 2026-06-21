using System.Text.RegularExpressions;

namespace ServiceLayer.Services;

public class SentenceChunkingStrategy : IChunkingStrategy
{
    public string Name => "Sentence";

    public List<(string Text, int Page)> Chunk(List<(int Page, string Text)> pages)
    {
        var chunks = new List<(string Text, int Page)>();

        foreach (var (page, text) in pages)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;

            // Tách theo dấu chấm, chấm hỏi, chấm than (kèm khoảng trắng)
            var sentences = Regex.Split(text, @"(?<=[\.!\?])\s+")
                                 .Select(s => s.Trim())
                                 .Where(s => s.Length > 20) // Bỏ qua câu quá ngắn
                                 .ToList();

            foreach (var s in sentences)
            {
                chunks.Add((s, page));
            }
        }

        return chunks;
    }
}


