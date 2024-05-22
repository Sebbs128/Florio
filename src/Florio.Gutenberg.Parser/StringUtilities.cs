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
                .Replace(Constants.Close_O_Upper, "O")
                .Replace(Constants.Close_O_Lower, "o");
        }

        /// <summary>
        /// Converts a string to a plain ASCII form by removing accents, 
        /// and symbols the Project Gutenberg transcribers added to denote pronunciation
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string Normalize(string input)
        {
            return GetPrintableString(new string(input
                .Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray()))
                .ToLowerInvariant();
        }
    }
}
