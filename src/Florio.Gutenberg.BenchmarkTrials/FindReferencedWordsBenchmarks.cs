using System;
using System.Collections.Generic;
using System.Linq;

using BenchmarkDotNet.Attributes;

namespace Florio.Gutenberg.BenchmarkTrials
{
    [MemoryDiagnoser]
    public class FindReferencedWordsBenchmarks
    {
        public string[] Definitions =>
        [
            "_as_ Abbacáre.",
            "_as_ A sácc[o].",
            "_as_ Arctúr[o]. _Also the Clot-bur._",
            "_as_ Arecáre, _to reach vnto._",
            "_as_ Athẻísta, _or_ Athẻ[o].",
            "_as_ Mascalz[ó]ne, _as_ Bárr[o].",
            "_vsed for_ Biéc[o], _as_ Biócc[o]li.",
            "_as_ Bótta, Di bótt[o], _quickly. Also a stroke._",
            "_as_ Fẻccia, _or as_ Gr[ó]mma.",
            "_as_ Cauagliẻre, &c.",
        ];

        [ParamsSource(nameof(Definitions))]
        public string Data;

        [Benchmark]
        public List<string> ForLoop()
        {
            return MethodToTest(Data).ToList();

            static IEnumerable<string> MethodToTest(string definition)
            {
                var parts = definition.Split(
                    '_',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                for (int i = 1; i < parts.Length; i += 2)
                {
                    yield return parts[i]
                        .Replace("&c", "")
                        .Trim([' ', ',', '.']);
                }
            }
        }

        [Benchmark]
        public List<string> Linq()
        {
            return MethodToTest(Data).ToList();

            static IEnumerable<string> MethodToTest(string definition)
            {
                return definition
                .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where((_, idx) => idx % 2 == 1)
                .Select(s => s.Replace("&c", "").Trim([' ', ',', '.']));
            }
        }
    }
}
