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
    // TODO: can't limit to just ASCII letters here, because this gets used for showing autocomplete results
    //   ASCII letters brings vector size down under 4096, which is the max cosmosdb allows.
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

    string NormalizeForVector(string input) =>
        new(input
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD)
            .Where(c => char.IsAsciiLetterLower(c) || c is ' ')
            .ToArray());

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
