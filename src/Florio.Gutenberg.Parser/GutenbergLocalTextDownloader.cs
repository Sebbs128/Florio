using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Florio.Gutenberg.Parser
{
    public class GutenbergLocalTextDownloader : IGutenbergTextDownloader
    {
        private const string _filePathPart = @".localassets\pg56200.txt";
        private readonly string _filePath;

        public GutenbergLocalTextDownloader(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));

            if (!File.Exists(filePath))
            {
                ThrowFileNotFoundException(filePath);
            }
            _filePath = filePath;
        }

        public async IAsyncEnumerable<string> ReadLines([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            using var stream = File.OpenRead(_filePath);
            using var reader = new StreamReader(stream);
            while (!(reader.EndOfStream || cancellationToken.IsCancellationRequested))
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is not null)
                    yield return line;
            }
        }

        [DoesNotReturn]
        private static void ThrowFileNotFoundException(string path)
        {
            throw new FileNotFoundException("A file was not found at the given path.", path);
        }
    }
}
