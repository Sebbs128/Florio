namespace Florio.Data;

public record struct WordDefinition(string Word, string Definition) : IEquatable<WordDefinition>
{
    public string[]? ReferencedWords { get; set; } = [];

    public override readonly int GetHashCode() => HashCode.Combine(Word, Definition);

    public readonly bool Equals(WordDefinition other) =>
        string.Equals(Word, other.Word, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Definition, other.Definition, StringComparison.OrdinalIgnoreCase);
}
