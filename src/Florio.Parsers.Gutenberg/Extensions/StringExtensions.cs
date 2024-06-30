namespace Florio.Parsers.Gutenberg.Extensions;
internal static class StringExtensions
{
    public static string CapitaliseFirstLetter(this string str) =>
        $"{ToUpper(str[0])}{str[1..]}";

    private static string ToUpper(char c) =>
        c is 'u' ? "V"
            : char.ToLowerInvariant(c) is 'u' ? "V`"
            : char.ToUpperInvariant(c).ToString();
}
