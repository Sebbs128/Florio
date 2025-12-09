namespace Florio.Parsers.Gutenberg.Extensions;

internal static class StringExtensions
{
    extension(ReadOnlySpan<char> value)
    {
        public string CapitaliseFirstLetter()
        {
            int length = value.Length;
            if (char.ToLowerInvariant(value[0]) is 'ù' or 'ú')
            {
                length++;
            }
            return string.Create(length, value, (span, chars) =>
            {
                int i = 0;
                // if starts with "u", change to "V"
                if (chars[0] is 'u')
                {
                    span[0] = 'V';
                    i++;
                }
                // cases with diacritics should be rare,
                // but if they're ever found they should be printed as "V`"
                else if (char.ToLowerInvariant(chars[0]) is 'ù' or 'ú')
                {
                    "V`".CopyTo(span[0..1]);
                    i += 2;
                }
                // anything else we just convert to uppercase
                else
                {
                    span[0] = char.ToUpperInvariant(chars[0]);
                    i += 1;
                }

                chars[1..].CopyTo(span[i..]);
            });
        }
    }
}
