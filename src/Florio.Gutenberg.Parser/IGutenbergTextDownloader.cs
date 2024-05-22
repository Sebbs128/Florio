namespace Florio.Gutenberg.Parser
{
    public interface IGutenbergTextDownloader
    {
        IAsyncEnumerable<string> ReadLines(CancellationToken cancellationToken = default);
    }
}