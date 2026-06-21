namespace ServiceLayer.Services.Embeddings;

public interface IEmbeddingProvider
{
    string Name { get; }
    Task<float[]> GetEmbeddingAsync(string text, string model);
}

public interface IEmbeddingFactory
{
    IEmbeddingProvider? GetProvider(string name);
}

public class EmbeddingFactory : IEmbeddingFactory
{
    private readonly IEnumerable<IEmbeddingProvider> _providers;

    public EmbeddingFactory(IEnumerable<IEmbeddingProvider> providers)
    {
        _providers = providers;
    }

    public IEmbeddingProvider? GetProvider(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "Keyword") return null;
        return _providers.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
