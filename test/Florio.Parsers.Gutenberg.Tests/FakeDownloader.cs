using System.Runtime.CompilerServices;
using System.Text;

namespace Florio.Parsers.Gutenberg.Tests;

public class FakeDownloader(string input) : IGutenbergTextDownloader
{
    private readonly string _input = input;

    public async IAsyncEnumerable<string> ReadLines([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(_input));
        var reader = new StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null && !cancellationToken.IsCancellationRequested)
        {
            yield return line;
        }
    }
}