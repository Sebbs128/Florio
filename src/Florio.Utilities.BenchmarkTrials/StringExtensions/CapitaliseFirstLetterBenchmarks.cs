using System;

using BenchmarkDotNet.Attributes;

namespace Florio.Utilities.BenchmarkTrials.StringExtensions;

[MemoryDiagnoser]
public class CapitaliseFirstLetterBenchmarks
{
    private static readonly string[] _testWords =
    [
        "affielíre",
        "affielísc[o]",
        "affielít[o]",
        "audíre",
        "ód[o]",
        "udíj",
        "udít[o]",
        "s[o]prandáre",
        "s[o]prauád[o]",
        "s[o]prandái",
        "s[o]prandát[o]",
        "variábile"
    ];

    [Benchmark]
    public string[] StringExtension()
    {
        var result = new string[_testWords.Length];
        for (int i = 0; i < _testWords.Length; i++)
        {
            result[i] = CapitaliseFirstLetter_String(_testWords[i]);
        }

        return result;
    }

    [Benchmark]
    public string[] ReadOnlySpanExtension()
    {
        var result = new string[_testWords.Length];
        for (int i = 0; i < _testWords.Length; i++)
        {
            result[i] = CapitaliseFirstLetter_Span(_testWords[i]);
        }

        return result;
    }

    private static string CapitaliseFirstLetter_String(string value) => $"{ToUpper(value[0])}{value.AsSpan(1)}";

    private static string ToUpper(char c) =>
        c is 'u' ? "V"
            : char.ToLowerInvariant(c) is 'u' ? "V`"
            : char.ToUpperInvariant(c).ToString();

    private static string CapitaliseFirstLetter_Span(ReadOnlySpan<char> value)
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
