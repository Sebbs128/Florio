namespace Florio.VectorEmbeddings.Repositories;

public interface IRepositoryMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}