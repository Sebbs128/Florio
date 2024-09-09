using Florio.Data;

namespace Florio.VectorEmbeddings.Repositories;
public interface IWordDefinitionRepository
{
    /// <summary>
    /// Creates the collection if it didn't already exist,
    /// and returns whether the collection existed prior to calling the method.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>true if the collection existed; otherwise false.</returns>
    Task<bool> CollectionExists(CancellationToken cancellationToken = default);
    IAsyncEnumerable<WordDefinition> FindByWord(ReadOnlyMemory<float> vector, CancellationToken cancellationToken = default);
    IAsyncEnumerable<WordDefinition> FindClosestMatch(ReadOnlyMemory<float> vector, CancellationToken cancellationToken = default);
    IAsyncEnumerable<WordDefinition> FindMatches(ReadOnlyMemory<float> vector, int limit = 10, CancellationToken cancellationToken = default);
}
