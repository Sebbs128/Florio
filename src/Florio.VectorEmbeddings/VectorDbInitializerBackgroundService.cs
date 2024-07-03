using Florio.VectorEmbeddings.Settings;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Florio.VectorEmbeddings;

public class VectorDbInitializerBackgroundService(
    IHostApplicationLifetime hostApplicationLifetime,
    VectorDbInitializer vectorDbInitializer,
    VectorDbInitializerSettings settings,
    ILogger<VectorDbInitializerBackgroundService> logger)
    : BackgroundService
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime = hostApplicationLifetime;
    private readonly VectorDbInitializer _vectorDbInitializer = vectorDbInitializer;
    private readonly VectorDbInitializerSettings _settings = settings;
    private readonly ILogger<VectorDbInitializerBackgroundService> _logger = logger;

    public bool VectorDbInitCompleted { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking if vector database has been initialized.");

        if (!await _vectorDbInitializer.CheckDatabaseInitializedAsync(cancellationToken))
        {
            _logger.LogInformation("Vector database is not initialized.");

            await _vectorDbInitializer.InitializeDatabaseAsync(cancellationToken);
        }

        if (_settings.ShutdownAfterFinish)
        {
            _logger.LogInformation("Shutdown after finish is set.");
            _hostApplicationLifetime.StopApplication();
        }
    }
}
