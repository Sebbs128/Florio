namespace Florio.Parsers.Gutenberg;

public interface IGutenbergTextDownloader
{
    IAsyncEnumerable<string> ReadLines(CancellationToken cancellationToken = default);
}