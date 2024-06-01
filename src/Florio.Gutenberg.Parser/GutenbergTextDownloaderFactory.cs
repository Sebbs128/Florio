namespace Florio.Gutenberg.Parser
{
    public class GutenbergTextDownloaderFactory
    {
        private readonly string? _partialLocalFilePath;
        private readonly GutenbergTextDownloader _httpDownloader;

        // TODO: not happy with this
        public GutenbergTextDownloaderFactory(GutenbergTextDownloader httpDownloader, string? partialLocalFilePath)
        {
            _httpDownloader = httpDownloader;
            _partialLocalFilePath = partialLocalFilePath;
        }

        public IGutenbergTextDownloader GetDownloader()
        {
            if (!string.IsNullOrWhiteSpace(_partialLocalFilePath))
            {
                if ((Path.IsPathFullyQualified(_partialLocalFilePath) || Path.IsPathRooted(_partialLocalFilePath))
                        && File.Exists(_partialLocalFilePath))
                {
                    return new GutenbergLocalTextDownloader(_partialLocalFilePath);
                }

                var directory = Environment.CurrentDirectory;
                while (directory is not null && !File.Exists(Path.Combine(directory, _partialLocalFilePath)))
                {
                    directory = Directory.GetParent(directory)?.FullName;
                }

                if (directory is not null)
                {
                    return new GutenbergLocalTextDownloader(Path.Combine(directory, _partialLocalFilePath));
                }
            }

            return _httpDownloader;
        }
    }
}
