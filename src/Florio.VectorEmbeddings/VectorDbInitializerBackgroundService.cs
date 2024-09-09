using System.Diagnostics;

using Florio.VectorEmbeddings.Repositories;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry.Trace;

namespace Florio.VectorEmbeddings;

public class VectorDbInitializerBackgroundService(
    IRepositoryMigrator migrator,
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<VectorDbInitializerBackgroundService> logger)
    : BackgroundService
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource _activitySource = new(ActivitySourceName);

    private readonly IRepositoryMigrator _migrator = migrator;
    private readonly IHostApplicationLifetime _hostApplicationLifetime = hostApplicationLifetime;
    private readonly ILogger<VectorDbInitializerBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("Migrating vector database", ActivityKind.Client);

        try
        {
            await _migrator.MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            throw;
        }

        _hostApplicationLifetime.StopApplication();
    }
}
