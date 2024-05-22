using System.Runtime.CompilerServices;
using System.Text;

namespace Florio.Gutenberg.Parser.Tests
{
    public class FakeDownloader(string input) : IGutenbergTextDownloader
        {
            private readonly string _input = input;

            public async IAsyncEnumerable<string> ReadLines([EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(_input));
                var reader = new StreamReader(stream);
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (line is not null)
                        yield return line;
                }
            }
        }
}