namespace ServiceLayer.Services;

public interface ITextExtractor
{
    // Returns a list of (pageNumber, text). For non-paged formats, single entry with page=1.
    List<(int Page, string Text)> Extract(Stream stream, string fileName, string contentType);
}


