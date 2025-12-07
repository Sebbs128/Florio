using Florio.Data;

namespace Florio.Parsers.Gutenberg;
public class StringFormatter : IStringFormatter
{
    /// <summary>
    /// Converts a string to a form suitable for printing by
    /// removing symbols the Project Gutenberg transcribers added to denote pronunciation
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public string ToPrintableString(ReadOnlySpan<char> input)
    {
        Span<char> buffer = stackalloc char[input.Length];
        var pos = 0;

        foreach (var c in input.SplitAny('[', ']'))
        {
            input[c].CopyTo(buffer[pos..]);
            pos += c.End.Value - c.Start.Value;
        }

        return new string(buffer[..pos]);
    }
}
