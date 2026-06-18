namespace ServiceLayer.Services;

public interface IChunker
{
    List<(string Text, int Page)> Chunk(List<(int Page, string Text)> pages);
}
