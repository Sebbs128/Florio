using Florio.Data;

namespace Florio.VectorEmbeddings.Repositories;

public record struct WordDefinitionEmbedding(
    ReadOnlyMemory<float> Vector,
    WordDefinition WordDefinition);