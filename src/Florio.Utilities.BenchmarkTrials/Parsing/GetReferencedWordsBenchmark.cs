using System;
using System.Collections.Generic;
using System.Linq;

using BenchmarkDotNet.Attributes;

using Florio.Parsers.Gutenberg.Extensions;

namespace Florio.Utilities.BenchmarkTrials.Parsing;

[MemoryDiagnoser]
public class GetReferencedWordsBenchmark
{
    private static readonly string _data =
        "_a word much vsed in composition of other nounes to expresse littlenesse and prettinesse withall, as_ Librétt[o], Hométt[o], C[o]sétta, Casétta, &c.";

    [Benchmark(Baseline = true)]
    public int String()
    {
        return GetReferencedWords_String(_data).Count();
    }

    private IEnumerable<string> GetReferencedWords_String(string definition)
    {
        var parts = definition.Split('_',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 1; i < parts.Length; i += 2)
        {
            if (parts[i].StartsWith("a, b, c", StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            var part = parts[i].Trim([',', '.']);

            if (part.IndexOf(',') > 0 &&
                (part.Count(c => c == ' ') == part.Count(c => c == ',') ||
                 part.StartsWith("Picchi[ó]ne", StringComparison.Ordinal)))
            {
                // these should all be cases where multiple examples are given.
                // we can split by ',' and return them
                // also split by '.' to split sentence examples

                var examples = parts[i].Split([',', '.'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var example in examples)
                {
                    if (example.StartsWith("&c"))
                    {
                        continue;
                    }
                    yield return example.Trim([' ', ',', ':', ';', '.']).AsSpan().CapitaliseFirstLetter();
                }

                continue;
            }

            if (part.Contains('.') && part is not [.., '.'] &&
                part.IndexOf(' ') > part.IndexOf('.'))
            {
                foreach (var example in part.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    yield return example.Trim([' ', ',']);
                }
                continue;
            }

            yield return part
                .Replace("&c", "", StringComparison.OrdinalIgnoreCase)
                .Trim([' ', ',', ':', ';', '.']);
        }
    }

    [Benchmark]
    public int Span_Yield()
    {
        return GetReferencedWords_SpanYield(_data).Count();
    }

    private IEnumerable<string> GetReferencedWords_SpanYield(string definition)
    {
        // ReadOnlySpans won't survive across yield boundaries, so this helper makes it easier to repopulate it
        static ReadOnlySpan<char> GetDefinitionAsSpan(string definition, Range range) =>
            definition.AsSpan(range).Trim([',', '.']);

        var partsRanges = new Memory<Range>(new Range[definition.Count('_')]);
        var partsLength = definition.Split(partsRanges.Span, '_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 1; i < partsLength; i += 2)
        {
            var range = partsRanges.Span[i];
            if (definition[range].StartsWith("a, b, c", StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            var part = GetDefinitionAsSpan(definition, range);

            if (part.IndexOf(',') > 0 &&
                (part.Count(' ') == part.Count(',') ||
                 part.StartsWith("Picchi[ó]ne", StringComparison.Ordinal)))
            {
                // these should all be cases where multiple examples are given.
                // we can split by ',' and return them
                // also split by '.' to split sentence examples

                var exampleRanges = new Memory<Range>(new Range[part.CountAny([',', '.']) + 1]);
                var length = part.SplitAny(exampleRanges.Span, [',', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                for (int j = 0; j < length; j++)
                {
                    var exampleRange = exampleRanges.Span[j];
                    var example = part[exampleRange];
                    if (example.StartsWith("&c"))
                    {
                        continue;
                    }
                    yield return new string(example.Trim([' ', ',', ':', ';', '.']).CapitaliseFirstLetter());
                    part = GetDefinitionAsSpan(definition, range);
                }

                continue;
            }

            if (part.Contains('.') && part is not [.., '.'] &&
                part.IndexOf(' ') > part.IndexOf('.'))
            {
                var exampleRanges = new Memory<Range>(new Range[part.Count('.') + 1]);
                var length = part.Split(exampleRanges.Span, '.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                for (int j = 0; j < length; j++)
                {
                    var exampleRange = exampleRanges.Span[j];
                    yield return new string(part[exampleRange].Trim([' ', ',']));
                    part = GetDefinitionAsSpan(definition, range);
                }
                continue;
            }

            yield return new string(part)
                .Replace("&c", "", StringComparison.OrdinalIgnoreCase)
                .Trim([' ', ',', ':', ';', '.']);
        }
    }

    [Benchmark]
    public int Span_NoYield()
    {
        return GetReferencedWords_SpanNoYield(_data).Count;
    }

    private List<string> GetReferencedWords_SpanNoYield(ReadOnlySpan<char> definition)
    {
        var results = new List<string>();

        Span<Range> partsRanges = stackalloc Range[definition.Count('_')];
        var partsLength = definition.Split(partsRanges, '_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 1; i < partsLength; i += 2)
        {
            var range = partsRanges[i];
            if (definition[range].StartsWith("a, b, c", StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            var part = definition[range].Trim([',', '.']);

            if (part.IndexOf(',') > 0 &&
                (part.Count(' ') == part.Count(',') ||
                 part.StartsWith("Picchi[ó]ne", StringComparison.Ordinal)))
            {
                // these should all be cases where multiple examples are given.
                // we can split by ',' and return them
                // also split by '.' to split sentence examples

                foreach (var exampleRange in part.SplitAny(",."))
                {
                    var example = part[exampleRange].Trim();
                    if (example.IsEmpty)
                    {
                        continue;
                    }
                    if (example.StartsWith("&c"))
                    {
                        continue;
                    }
                    results.Add(new(example.Trim([' ', ',', ':', ';', '.']).CapitaliseFirstLetter()));
                }

                continue;
            }

            if (part.Contains('.') && part is not [.., '.'] &&
                part.IndexOf(' ') > part.IndexOf('.'))
            {
                foreach (var exampleRange in part.Split('.'))
                {
                    var example = part[exampleRange].Trim();
                    if (example.IsEmpty)
                    {
                        continue;
                    }
                    results.Add(new(part[exampleRange].Trim([' ', ','])));
                }

                continue;
            }

            results.Add(new string(part)
                .Replace("&c", "", StringComparison.OrdinalIgnoreCase)
                .Trim([' ', ',', ':', ';', '.']));
        }

        return results;
    }
}
