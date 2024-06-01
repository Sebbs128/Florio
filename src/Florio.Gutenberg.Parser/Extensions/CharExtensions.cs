using System.Globalization;

namespace Florio.Gutenberg.Parser.Extensions
{
    internal static class CharExtensions
    {
        public static bool IsNonSpacingMark(this char c) =>
            CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark;
    }
}
