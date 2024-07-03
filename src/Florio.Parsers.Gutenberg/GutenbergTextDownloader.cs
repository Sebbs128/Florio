using System.Runtime.CompilerServices;

namespace Florio.Parsers.Gutenberg;

public class GutenbergTextDownloader : IGutenbergTextDownloader
{
    private readonly HttpClient _httpClient;

    public GutenbergTextDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async IAsyncEnumerable<string> ReadLines([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            yield break;
        }

        using var stream = await _httpClient.GetStreamAsync(Constants.Gutenberg_Text_Url);
        using var reader = new StreamReader(stream);
        while (!(reader.EndOfStream || cancellationToken.IsCancellationRequested))
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is not null)
                yield return line;
        }
    }
}
