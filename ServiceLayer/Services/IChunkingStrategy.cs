namespace ServiceLayer.Services;

public interface IChunkingStrategy
{
    string Name { get; }
    List<(string Text, int Page)> Chunk(List<(int Page, string Text)> pages);
}

public interface IChunkingFactory
{
    IChunkingStrategy GetStrategy(string name);
}
