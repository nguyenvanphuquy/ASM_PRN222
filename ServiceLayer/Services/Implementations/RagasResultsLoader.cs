using System.Text.RegularExpressions;
using ServiceLayer.Dtos;

namespace ServiceLayer.Services.Implementations;

/// <summary>Đọc kết quả RAGAS từ eval/RAGAS_Results.md (nếu có).</summary>
public static class RagasResultsLoader
{
    private static readonly Regex MetricLine = new(
        @"^\|\s*(faithfulness|answer_relevancy|context_precision|context_recall)\s*\|\s*\*\*([0-9.]+)\*\*",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    public static RagasSummaryDto? TryLoad()
    {
        var path = FindFile(AppContext.BaseDirectory) ?? FindFile(Directory.GetCurrentDirectory());
        if (path == null || !File.Exists(path)) return null;

        var text = File.ReadAllText(path);
        var matches = MetricLine.Matches(text);
        if (matches.Count < 4) return null;

        var summary = new RagasSummaryDto { HasData = true };
        foreach (Match m in matches)
        {
            var key = m.Groups[1].Value.ToLowerInvariant();
            if (!double.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var val))
                continue;

            switch (key)
            {
                case "faithfulness": summary.Faithfulness = val; break;
                case "answer_relevancy": summary.AnswerRelevancy = val; break;
                case "context_precision": summary.ContextPrecision = val; break;
                case "context_recall": summary.ContextRecall = val; break;
            }
        }

        if (text.Contains("SWE301", StringComparison.OrdinalIgnoreCase))
            summary.Subject = "SWE301";

        var qMatch = Regex.Match(text, @"Số câu:\*\*\s*(\d+)|-\s*\*\*Số câu:\*\*\s*(\d+)", RegexOptions.IgnoreCase);
        if (qMatch.Success)
        {
            var qStr = qMatch.Groups[1].Success ? qMatch.Groups[1].Value : qMatch.Groups[2].Value;
            if (int.TryParse(qStr, out var q)) summary.Questions = q;
        }

        return summary;
    }

    private static string? FindFile(string start)
    {
        var dir = new DirectoryInfo(start);
        for (var i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
        {
            var p = Path.Combine(dir.FullName, "eval", "RAGAS_Results.md");
            if (File.Exists(p)) return p;
        }
        return null;
    }
}
