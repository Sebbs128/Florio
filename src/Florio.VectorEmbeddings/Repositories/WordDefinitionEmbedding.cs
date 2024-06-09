using Florio.Data;

namespace Florio.VectorEmbeddings.Repositories;

public record struct WordDefinitionEmbedding(ReadOnlyMemory<float> Vector, WordDefinition WordDefinition)
{
    public static implicit operator (ReadOnlyMemory<float> Vector, WordDefinition WordDefinition)(WordDefinitionEmbedding value) => (value.Vector, value.WordDefinition);
    public static implicit operator WordDefinitionEmbedding((ReadOnlyMemory<float> Vector, WordDefinition WordDefinition) value) => new(value.Vector, value.WordDefinition);
}