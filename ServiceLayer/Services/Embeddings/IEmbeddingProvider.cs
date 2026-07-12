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
        if (string.IsNullOrWhiteSpace(name) || name.Equals("Keyword", StringComparison.OrdinalIgnoreCase))
            return null;

        // Match by provider name ("OpenAI", "HuggingFace") or by model id from Settings.
        var direct = _providers.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (direct != null) return direct;

        var providerName = name switch
        {
            "text-embedding-3-small" => "OpenAI",
            // 3 model mở chạy qua sidecar sentence-transformers local (không cần key).
            "PhoBERT-base" or "bge-m3" or "multilingual-e5-base" => "LocalST",
            _ when name.StartsWith("text-embedding", StringComparison.OrdinalIgnoreCase) => "OpenAI",
            _ => null
        };

        return providerName == null
            ? null
            : _providers.FirstOrDefault(p =>
                string.Equals(p.Name, providerName, StringComparison.OrdinalIgnoreCase));
    }
}
