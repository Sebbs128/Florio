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
    public string ToPrintableString(string input) =>
        input.Replace("[", "").Replace("]", "");
}
