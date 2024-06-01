using System.Globalization;
using System.Text;

namespace Florio.Gutenberg.Parser
{
    public class StringUtilities
    {
        /// <summary>
        /// Converts a string to a form suitable for printing by
        /// removing symbols the Project Gutenberg transcribers added to denote pronunciation
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string GetPrintableString(string input)
        {
            return input
                .Replace("[", "").Replace("]", "");
        }

        /// <summary>
        /// Converts a string to a plain ASCII form by removing diacritics
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string GetNormalizedString(string input)
        {
            return new string(input
                .Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());
        }

        /// <summary>
        /// Converts a string to a plain ASCII form by removing diacritics, 
        /// and symbols the Project Gutenberg transcribers added to denote pronunciation
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string GetPrintableNormalizedString(string input)
        {
            return GetPrintableString(
                GetNormalizedString(input))
                .ToLowerInvariant();
        }
    }
}
