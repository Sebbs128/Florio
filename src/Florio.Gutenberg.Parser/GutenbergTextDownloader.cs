using System.Runtime.CompilerServices;

namespace Florio.Gutenberg.Parser
{
    public class GutenbergTextDownloader : IGutenbergTextDownloader
    {
        private readonly HttpClient _httpClient;

        public GutenbergTextDownloader(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async IAsyncEnumerable<string> ReadLines([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var stream = await _httpClient.GetStreamAsync(Constants.Gutenberg_Text_Url);
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is not null)
                    yield return line;
            }
        }
    }
}
