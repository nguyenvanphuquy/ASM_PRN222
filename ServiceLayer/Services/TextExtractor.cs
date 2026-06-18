using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace ServiceLayer.Services;

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

        // Plain text fallback
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        return new List<(int, string)> { (1, text) };
    }

    private static List<(int Page, string Text)> ExtractPdf(Stream stream)
    {
        var pages = new List<(int, string)>();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        using var pdf = PdfDocument.Open(ms);
        int i = 1;
        foreach (var p in pdf.GetPages())
        {
            pages.Add((i, p.Text ?? string.Empty));
            i++;
        }
        return pages;
    }

    private static List<(int Page, string Text)> ExtractDocx(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        using var word = WordprocessingDocument.Open(ms, false);
        var body = word.MainDocumentPart?.Document.Body;
        if (body is null) return new List<(int, string)> { (1, string.Empty) };

        var paragraphs = body.Descendants<Paragraph>().Select(p => p.InnerText);
        var text = string.Join("\n", paragraphs);
        return new List<(int, string)> { (1, text) };
    }

    private static List<(int Page, string Text)> ExtractPptx(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        using var ppt = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(ms, false);
        var presentationPart = ppt.PresentationPart;
        if (presentationPart is null) return new List<(int, string)> { (1, string.Empty) };

        if (presentationPart.Presentation?.SlideIdList is null) return new List<(int, string)> { (1, string.Empty) };

        var pages = new List<(int, string)>();
        int i = 1;
        foreach (var slideId in presentationPart.Presentation.SlideIdList.Elements<DocumentFormat.OpenXml.Presentation.SlideId>())
        {
            var relId = slideId.RelationshipId?.Value;
            if (string.IsNullOrEmpty(relId)) continue;

            var slidePart = presentationPart.GetPartById(relId) as DocumentFormat.OpenXml.Packaging.SlidePart;
            if (slidePart?.Slide != null)
            {
                var texts = slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>().Select(t => t.Text);
                pages.Add((i, string.Join("\n", texts)));
            }
            else
            {
                pages.Add((i, string.Empty));
            }
            i++;
        }
        return pages.Count > 0 ? pages : new List<(int, string)> { (1, string.Empty) };
    }
}
