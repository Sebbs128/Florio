using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ML;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.ML;

namespace Florio.VectorEmbeddings.EmbeddingsModel;
internal class OnnxModelLoader<TInputModel> : ModelLoader, IDisposable where TInputModel : class
{
    private readonly MLContext _context;
    private readonly ILogger<OnnxModelLoader<TInputModel>> _logger;
    private readonly object _lock;

    private string? _filePath;
    private FileSystemWatcher? _watcher;
    private ModelReloadToken? _reloadToken;
    private ITransformer? _model;

    public OnnxModelLoader(IOptions<MLOptions> contextOptions, ILogger<OnnxModelLoader<TInputModel>> logger)
    {
        ArgumentNullException.ThrowIfNull(contextOptions?.Value, nameof(contextOptions));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        _context = contextOptions.Value!.MLContext;
        _logger = logger;
        _lock = new object();
    }

    public void Start(string filePath, bool watchFile)
    {
        _filePath = filePath;
        _reloadToken = new();

        if (!File.Exists(filePath))
        {
            throw new ArgumentException($"The provided model file {filePath} doesn't exist.");
        }

        var directory = Path.GetDirectoryName(filePath);

        if (string.IsNullOrEmpty(directory))
        {
            directory = Directory.GetCurrentDirectory();
        }

        var file = Path.GetFileName(filePath);

        LoadModel();

        if (watchFile)
        {
            _watcher = new FileSystemWatcher(directory, file)
            {
                EnableRaisingEvents = true
            };
            _watcher.Changed += WatcherChanged;
        }
    }

    private void WatcherChanged(object sender, FileSystemEventArgs e)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            Logger.FileReloadBegin(_logger, _filePath!);

            var previousToken = Interlocked.Exchange(ref _reloadToken, new ModelReloadToken());
            lock (_lock)
            {
                LoadModel();
                Logger.ReloadingFile(_logger, _filePath!, timer.Elapsed);
            }
            previousToken?.OnReload();
            timer.Stop();
            Logger.FileReloadEnd(_logger, _filePath!, timer.Elapsed);
        }
        catch (OperationCanceledException)
        {
            // This is a cancellation - if the app is shutting down we want to ignore it.
        }
        catch (Exception ex)
        {
            Logger.FileReloadError(_logger, _filePath!, timer.Elapsed, ex);
        }
    }

    public override IChangeToken GetReloadToken() =>
        _reloadToken ?? throw new InvalidOperationException("Start must be called on a ModelLoader before it can be used.");

    public override ITransformer GetModel() =>
        _model ?? throw new InvalidOperationException("Start must be called on a ModelLoader before it can be used.");

    private void LoadModel()
    {
        var dataView = _context.Data.LoadFromEnumerable(Enumerable.Empty<TInputModel>());

        var embeddingPipeline = _context.Transforms.ApplyOnnxModel(_filePath);
        _model = embeddingPipeline.Fit(dataView);
    }

    public void Dispose() => _watcher?.Dispose();

    internal static class EventIds
    {
        public static readonly EventId FileReloadBegin = new(100, "FileReloadBegin");
        public static readonly EventId FileReloadEnd = new(101, "FileReloadEnd");
        public static readonly EventId FileReload = new(102, "FileReload");
        public static readonly EventId FileReloadError = new(103, nameof(FileReloadError));
    }

    private static class Logger
    {
        private static readonly Action<ILogger, string, Exception?> _fileLoadBegin = LoggerMessage.Define<string>(
            LogLevel.Debug,
            EventIds.FileReloadBegin,
            "File reload for '{filePath}'");

        private static readonly Action<ILogger, string, double, Exception?> _fileLoadEnd = LoggerMessage.Define<string, double>(
            LogLevel.Debug,
            EventIds.FileReloadEnd,
            "File reload for '{filePath}' completed after {ElapsedMilliseconds}ms");

        private static readonly Action<ILogger, string, double, Exception> _fileReloadError = LoggerMessage.Define<string, double>(
            LogLevel.Error,
            EventIds.FileReloadError,
            "File reload for '{filePath}' threw an unhandled exception after {ElapsedMilliseconds}ms");

        private static readonly Action<ILogger, string, double, Exception?> _fileReLoad = LoggerMessage.Define<string, double>(
            LogLevel.Information,
            EventIds.FileReloadEnd,
            "Reloading file '{filePath}' completed after {ElapsedMilliseconds}ms");

        public static void FileReloadBegin(ILogger logger, string filePath)
        {
            _fileLoadBegin(logger, filePath, null);
        }

        public static void FileReloadEnd(ILogger logger, string filePath, TimeSpan duration)
        {
            _fileLoadEnd(logger, filePath, duration.TotalMilliseconds, null);
        }

        public static void FileReloadError(ILogger logger, string filePath, TimeSpan duration, Exception exception)
        {
            _fileReloadError(logger, filePath, duration.TotalMilliseconds, exception);
        }

        public static void ReloadingFile(ILogger logger, string filePath, TimeSpan duration)
        {
            _fileReLoad(logger, filePath, duration.TotalMilliseconds, null);
        }
    }

}
