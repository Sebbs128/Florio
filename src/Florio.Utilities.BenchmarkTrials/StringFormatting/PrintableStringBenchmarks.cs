using System;

using BenchmarkDotNet.Attributes;

namespace Florio.Utilities.BenchmarkTrials.StringFormatting;

[MemoryDiagnoser]
public class PrintableStringBenchmarks
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
        return input.Replace("[", "").Replace("]", "");
    }

    [Benchmark]
    [ArgumentsSource(nameof(Data))]
    public string Span(ReadOnlySpan<char> input)
    {
        Span<char> buffer = stackalloc char[input.Length];
        var pos = 0;

        foreach (var c in input.SplitAny('[', ']'))
        {
            input[c].CopyTo(buffer[pos..]);
            pos += c.End.Value - c.Start.Value;
        }

        return new string(buffer[..pos]);
    }
}
