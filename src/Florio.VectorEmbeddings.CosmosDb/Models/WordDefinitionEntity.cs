using Florio.Data;

namespace Florio.VectorEmbeddings.CosmosDb.Models;

#pragma warning disable IDE1006 // Naming Styles
internal record QueryResult(WordDefinitionDocument[] wordDefinitions);

internal record VectorGroupDocument(
    string id,
    float[] vector,
    string partitionKey,
    WordDefinitionDocument[] wordDefinitions
    );

internal record WordDefinitionDocument(
    string word,
    string definition,
    string[]? referencedWords)
{
    public static implicit operator WordDefinition(WordDefinitionDocument entity) => new(entity.word, entity.definition)
    {
        ReferencedWords = entity.referencedWords
    };
}
#pragma warning restore IDE1006 // Naming Styles
