using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ServiceLayer.Services.Implementations;

public class TextExtractor : ITextExtractor
{
    public List<(int Page, string Text)> Extract(Stream stream, string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (ext == ".pdf" || contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
            return ExtractPdf(stream);

        if (ext == ".docx" || contentType.Contains("officedocument.wordprocessingml", StringComparison.OrdinalIgnoreCase))
            return ExtractDocx(stream);

        if (ext == ".pptx" || contentType.Contains("officedocument.presentationml", StringComparison.OrdinalIgnoreCase))
            return ExtractPptx(stream);

        using var reader = new StreamReader(stream);
        return [(1, NormalizeText(reader.ReadToEnd()))];
    }

    private static List<(int Page, string Text)> ExtractPdf(Stream stream)
    {
        var pages = new List<(int, string)>();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        using var pdf = PdfDocument.Open(ms);
        int i = 1;
        foreach (var page in pdf.GetPages())
        {
            var fromWords = "";
            try
            {
                var words = page.GetWords().ToList();
                if (words.Count > 0)
                    fromWords = RebuildPdfText(words);
            }
            catch { /* fallback page.Text */ }

            var fromPage = page.Text ?? "";
            // Chọn nguồn có nhiều chữ cái/số hơn — tránh bản word-order lỗi tạo nhiều dòng trống.
            var a = NormalizeText(fromWords);
            var b = NormalizeText(fromPage);
            var best = CountAlphaNum(a) >= CountAlphaNum(b) ? a : b;

            if (!string.IsNullOrWhiteSpace(best))
                pages.Add((i, best));
            i++;
        }
        return pages;
    }

    /// <summary>
    /// Gom word theo hàng (bucket Y), đọc trái→phải, trên→dưới.
    /// </summary>
    private static string RebuildPdfText(IReadOnlyList<Word> words)
    {
        // Bucket ~4pt: cùng hàng gần như cùng Y.
        var lines = words
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .GroupBy(w => (int)Math.Round(w.BoundingBox.Bottom / 4.0))
            .OrderByDescending(g => g.Key) // PDF Y tăng từ dưới lên
            .Select(g => string.Join(" ",
                g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text.Trim())))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        return string.Join("\n", lines);
    }

    public static string NormalizeText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch is '\0' or '\u200b' or '\u200c' or '\u200d' or '\ufeff')
                continue;
            if (ch is '\u00a0' or '\t' or '\v')
            {
                sb.Append(' ');
                continue;
            }
            if (ch is '\f' or '\r' or '\u2028' or '\u2029')
            {
                sb.Append('\n');
                continue;
            }
            // Bỏ control chars khác (trừ \n)
            if (char.IsControl(ch) && ch != '\n')
                continue;
            sb.Append(ch);
        }

        var cleaned = sb.ToString();
        // Leader dots trong mục lục: "Topic ........ 12" → "Topic 12"
        cleaned = Regex.Replace(cleaned, @"\.{3,}", " ");
        cleaned = Regex.Replace(cleaned, @"[ \t]+", " ");
        // Mỗi dòng trim + bỏ dòng trống / chỉ dấu chấm số trang
        var lines = cleaned.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !Regex.IsMatch(l, @"^[\.\-–—\d\s]+$"))
            .ToList();

        cleaned = string.Join("\n", lines);
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");
        return cleaned.Trim();
    }

    public static int CountAlphaNum(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var n = 0;
        foreach (var c in text)
            if (char.IsLetterOrDigit(c)) n++;
        return n;
    }

    private static List<(int Page, string Text)> ExtractDocx(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        using var word = WordprocessingDocument.Open(ms, false);
        var body = word.MainDocumentPart?.Document.Body;
        if (body is null) return [(1, string.Empty)];

        var paragraphs = body.Descendants<Paragraph>().Select(p => p.InnerText);
        return [(1, NormalizeText(string.Join("\n", paragraphs)))];
    }

    private static List<(int Page, string Text)> ExtractPptx(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        using var ppt = PresentationDocument.Open(ms, false);
        var presentationPart = ppt.PresentationPart;
        if (presentationPart?.Presentation?.SlideIdList is null)
            return [(1, string.Empty)];

        var pages = new List<(int, string)>();
        int i = 1;
        foreach (var slideId in presentationPart.Presentation.SlideIdList.Elements<DocumentFormat.OpenXml.Presentation.SlideId>())
        {
            var relId = slideId.RelationshipId?.Value;
            if (string.IsNullOrEmpty(relId)) continue;

            var slidePart = presentationPart.GetPartById(relId) as SlidePart;
            if (slidePart?.Slide != null)
            {
                var texts = slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>().Select(t => t.Text);
                var normalized = NormalizeText(string.Join("\n", texts));
                if (!string.IsNullOrWhiteSpace(normalized))
                    pages.Add((i, normalized));
            }
            i++;
        }
        return pages.Count > 0 ? pages : [(1, string.Empty)];
    }
}
