using System;
using System.Globalization;
using System.Linq;
using System.Text;

using BenchmarkDotNet.Attributes;

namespace Florio.Utilities.BenchmarkTrials.StringFormatting;

[MemoryDiagnoser]
public class NormalizedStringBenchmarks
{
    public string[] Data =>
    [
        "Abachísta",
        "Apẻndi[o]",
        "Sẻttezz[ó]ni",
        "[O]bbróbri[o]",
        "Óbit[o]",
        "[Ó]ber[o]"
    ];

    [Benchmark(Baseline = true)]
    [ArgumentsSource(nameof(Data))]
    public string String(string input)
    {
        return new(input
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Where(c => c != '´')
            .ToArray());
    }

    [Benchmark]
    [ArgumentsSource(nameof(Data))]
    public string Span(string input)
    {
        var normalizedInputSpan = input.Normalize(NormalizationForm.FormD).AsSpan();
        Span<char> buffer = stackalloc char[input.Length];
        var destPos = 0;
        var fromSourcePos = 0;

        for (var sourcePos = 0; sourcePos < normalizedInputSpan.Length; sourcePos++)
        {
            var c = normalizedInputSpan[sourcePos];
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || c == '`')
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
}
