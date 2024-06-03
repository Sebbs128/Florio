using System.Globalization;
using System.Text;

namespace Florio.Gutenberg.Parser.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Converts a string to a form suitable for printing by
    /// removing symbols the Project Gutenberg transcribers added to denote pronunciation
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string GetPrintableString(this string input) =>
        input.Replace("[", "").Replace("]", "");

    /// <summary>
    /// Converts a string to a plain ASCII form by removing diacritics
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string GetNormalizedString(this string input) =>
        new(input
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Where(c => c != '´')
            .ToArray());

    /// <summary>
    /// Converts a string to a plain ASCII form by removing diacritics, 
    /// and symbols the Project Gutenberg transcribers added to denote pronunciation
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string GetPrintableNormalizedString(this string input) =>
        input.GetNormalizedString()
        .GetPrintableString()
        .ToLowerInvariant();
}
