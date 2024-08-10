namespace Florio.Parsers.Gutenberg.Extensions;
internal static class StringExtensions
{
    public static string CapitaliseFirstLetter(this string str) =>
        $"{ToUpper(str[0])}{str[1..]}";

    // if starts with "u", change to "V"
    // cases with diacritics (eg. 'Ú') should be rare
    private static string ToUpper(char c) =>
        c is 'u' ? "V"
            : char.ToLowerInvariant(c) is 'u' ? "V`"
            : char.ToUpperInvariant(c).ToString();
}
