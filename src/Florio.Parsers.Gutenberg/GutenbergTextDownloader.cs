using System.Runtime.CompilerServices;

namespace Florio.Parsers.Gutenberg;

public class GutenbergTextDownloader(HttpClient httpClient) : IGutenbergTextDownloader
{
    private readonly HttpClient _httpClient = httpClient;

    public async IAsyncEnumerable<string> ReadLines([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            yield break;
        }

        using var stream = await _httpClient.GetStreamAsync(Constants.Gutenberg_Text_Url, cancellationToken);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null && !cancellationToken.IsCancellationRequested)
        {
            yield return line;
        }
    }
}
