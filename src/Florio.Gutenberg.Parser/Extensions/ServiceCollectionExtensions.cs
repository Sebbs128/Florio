using Microsoft.Extensions.DependencyInjection;

namespace Florio.Gutenberg.Parser.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGutenbergDownloaderAndParser(this IServiceCollection services, string? localFilePath = null)
    {
        services.AddHttpClient<GutenbergTextDownloader>();

        services.AddSingleton<GutenbergTextDownloader>()
            .AddTransient<GutenbergTextParser>()
            .AddSingleton(sp => new GutenbergTextDownloaderFactory(
                sp.GetRequiredService<GutenbergTextDownloader>(),
                localFilePath))
            .AddTransient(sp =>
                sp.GetRequiredService<GutenbergTextDownloaderFactory>()
                .GetDownloader());

        return services;
    }
}
