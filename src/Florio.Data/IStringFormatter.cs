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
    public string ToNormalizedString(string input)
    {
        var normalizedInputSpan = input.Normalize(NormalizationForm.FormD).AsSpan();
        Span<char> buffer = stackalloc char[input.Length];
        var destPos = 0;
        var fromSourcePos = 0;

        for (var sourcePos = 0; sourcePos < normalizedInputSpan.Length; sourcePos++)
        {
            var c = normalizedInputSpan[sourcePos];
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || c == '´')
            {
                destPos += Copy(normalizedInputSpan[fromSourcePos..sourcePos], buffer[destPos..]);
                fromSourcePos = sourcePos + 1;
            }
        }

        if (fromSourcePos < normalizedInputSpan.Length)
        {
            destPos += Copy(normalizedInputSpan[fromSourcePos..], buffer[destPos..]);
        }

        return new string(buffer[..destPos]);

        static int Copy(ReadOnlySpan<char> from, Span<char> to)
        {
            from.CopyTo(to);
            return from.Length;
        }
    }

    /// <summary>
    /// Converts a string to a form suitable for printing by
    /// removing symbols transcribers added to denote pronunciation
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public string ToPrintableString(ReadOnlySpan<char> input);

    /// <summary>
    /// Converts a string to a normalized form for use by vector processing by
    /// converting to lowercase without diacritics, and removing characters that are not a letter or a space.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public string NormalizeForVector(string input)
    {
        var normalizedInputSpan = input.Normalize(NormalizationForm.FormD).AsSpan();
        Span<char> buffer = stackalloc char[input.Length];
        var destPos = 0;
        var fromSourcePos = 0;

        for (int sourcePos = 0; sourcePos < normalizedInputSpan.Length; sourcePos++)
        {
            var c = char.ToLowerInvariant(normalizedInputSpan[sourcePos]);
            if (!char.IsAsciiLetterLower(c) && c is not ' ')
            {
                destPos += normalizedInputSpan[fromSourcePos..sourcePos].ToLowerInvariant(buffer[destPos..]);
                fromSourcePos = sourcePos + 1;
            }
        }

        if (fromSourcePos < normalizedInputSpan.Length)
        {
            destPos += normalizedInputSpan[fromSourcePos..].ToLowerInvariant(buffer[destPos..]);
        }

        return new string(buffer[..destPos]);
    }

    /// <summary>
    /// Converts a string to a plain ASCII form by removing diacritics,
    /// and symbols transcribers added to denote pronunciation
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public string ToPrintableNormalizedString(string input) =>
        ToPrintableString(
            ToNormalizedString(input))
        .ToLowerInvariant();
}
