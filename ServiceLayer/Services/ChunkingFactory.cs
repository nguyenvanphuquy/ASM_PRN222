namespace ServiceLayer.Services;

public class ChunkingFactory : IChunkingFactory
{
    private readonly IEnumerable<IChunkingStrategy> _strategies;

    public ChunkingFactory(IEnumerable<IChunkingStrategy> strategies)
    {
        _strategies = strategies;
    }

    public IChunkingStrategy GetStrategy(string name)
    {
        var strategy = _strategies.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        return strategy ?? _strategies.First(s => s.Name == "SemanticKernel"); // Default
    }
}
