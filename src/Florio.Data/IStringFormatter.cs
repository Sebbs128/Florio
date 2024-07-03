using System.Globalization;
using System.Text;

namespace Florio.Data;
public interface IStringFormatter
{
    /// <summary>
    /// Converts a string to a plain ASCII form by removing diacritics
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    string ToNormalizedString(string input) =>
        new(input
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Where(c => c != '´')
            .ToArray());

    /// <summary>
    /// Converts a string to a form suitable for printing by
    /// removing symbols transcribers added to denote pronunciation
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    string ToPrintableString(string input);

    /// <summary>
    /// Converts a string to a plain ASCII form by removing diacritics,
    /// and symbols transcribers added to denote pronunciation
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    string ToPrintableNormalizedString(string input) =>
        ToPrintableString(
        ToNormalizedString(input))
        .ToLowerInvariant();
}
