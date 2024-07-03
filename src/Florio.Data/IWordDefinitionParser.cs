namespace Florio.Data;
public interface IWordDefinitionParser
{
    IAsyncEnumerable<WordDefinition> ParseLines(CancellationToken cancellationToken = default);
}