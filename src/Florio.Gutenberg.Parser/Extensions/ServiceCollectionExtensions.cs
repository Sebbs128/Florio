using System.Diagnostics.CodeAnalysis;

using Florio.Data;
using Florio.Gutenberg.Parser;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGutenbergDownloaderAndParser(this IServiceCollection services, string? localFilePath = null)
    {
        services.AddSingleton<IStringFormatter, StringFormatter>();

        if (LocalFilePathExists(localFilePath, out var resolvedFilePath))
        {
            services.AddSingleton<IGutenbergTextDownloader>(new GutenbergLocalTextDownloader(resolvedFilePath));
        }
        else
        {
            services.AddSingleton<IGutenbergTextDownloader, GutenbergTextDownloader>()
                .AddHttpClient<GutenbergTextDownloader>();
        }

        services.AddSingleton<IWordDefinitionParser, GutenbergTextParser>();

        return services;
    }

    private static bool LocalFilePathExists(string? filePath, [NotNullWhen(true)] out string? resolvedFilePath)
    {
        resolvedFilePath = null;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        if ((Path.IsPathFullyQualified(filePath) || Path.IsPathRooted(filePath)) && File.Exists(filePath))
        {
            resolvedFilePath = filePath;
            return true;
        }

        var directory = Environment.CurrentDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, filePath)))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        if (directory is not null)
        {
            resolvedFilePath = Path.Combine(directory, filePath);
            return true;
        }

        return false;
    }
}
