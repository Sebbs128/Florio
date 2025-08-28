namespace Florio.Data;

public sealed record WordDefinition(string Word, string Definition) : IEquatable<WordDefinition>
{
    public string[]? ReferencedWords { get; init; } = [];

    public override int GetHashCode() => HashCode.Combine(Word, Definition);

    public bool Equals(WordDefinition? other) =>
        other is not null &&
        string.Equals(Word, other.Word, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Definition, other.Definition, StringComparison.OrdinalIgnoreCase);
}
