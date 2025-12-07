using System;
using System.Linq;
using System.Text;

using BenchmarkDotNet.Attributes;

namespace Florio.Utilities.BenchmarkTrials.StringFormatting;

[MemoryDiagnoser]
public class NormalizeForVectorBenchmarks
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
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD)
            .Where(c => char.IsAsciiLetterLower(c) || c is ' ')
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
}
