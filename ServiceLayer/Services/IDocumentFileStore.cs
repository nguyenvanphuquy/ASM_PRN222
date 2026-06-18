namespace ServiceLayer.Services;

/// <summary>
/// Persists the original uploaded file bytes on disk so documents can be opened/downloaded
/// later. Files are keyed by document Id + original extension, so no DB schema change is needed.
/// </summary>
public interface IDocumentFileStore
{
    Task SaveAsync(string documentId, string fileName, Stream content);
    Stream? Open(string documentId, string fileName);
    bool Exists(string documentId, string fileName);
    void Delete(string documentId, string fileName);
}

public class LocalDocumentFileStore : IDocumentFileStore
{
    private readonly string _root;

    public LocalDocumentFileStore(string root)
    {
        _root = root;
        Directory.CreateDirectory(_root);
    }

    private string PathFor(string documentId, string fileName)
        => Path.Combine(_root, documentId + Path.GetExtension(fileName));

    public async Task SaveAsync(string documentId, string fileName, Stream content)
    {
        if (content.CanSeek) content.Position = 0;
        await using var fs = File.Create(PathFor(documentId, fileName));
        await content.CopyToAsync(fs);
    }

    public Stream? Open(string documentId, string fileName)
    {
        var path = PathFor(documentId, fileName);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public bool Exists(string documentId, string fileName)
        => File.Exists(PathFor(documentId, fileName));

    public void Delete(string documentId, string fileName)
    {
        var path = PathFor(documentId, fileName);
        if (File.Exists(path)) File.Delete(path);
    }
}
