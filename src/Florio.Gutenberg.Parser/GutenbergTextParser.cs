using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Florio.Gutenberg.Parser
{
    public class GutenbergTextParser(IGutenbergTextDownloader downloader)
    {
        private readonly IGutenbergTextDownloader _downloader = downloader;

        public async IAsyncEnumerable<WordDefinition> ParseLines([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var lineBuilder = new StringBuilder();

            var reachedEntries = false;
            var handlingDefinition = false;
            var previousDefinition = default(WordDefinition);
            var buffer = new Queue<string>();

            // TODO:
            // - ? handle "&c". "&c" is an old form of "etc"
            // - may need a buffer of lines for cases where the definition contains multiple examples containing '}' character
            await foreach (var line in _downloader.ReadLines(cancellationToken))
            {
                // handle end indicator
                // end indicator is "                                 FINIS."
                if (IsEndOfDefinitions(line))
                {
                    break;
                }

                // skip lines until we reach actual definitions ie. being after a heading such as "A"
                if (!reachedEntries)
                {
                    if (IsPageHeading(line))
                    {
                        reachedEntries = true;
                    }
                    continue;
                }

                //  ignore page headers ("A", "ABB" etc. anything not containing ", _")
                // they indicate that we've reach the section containing definitions though
                if (!handlingDefinition && ContainsDefinition(line))
                {
                    handlingDefinition = true;
                }

                if (!handlingDefinition)
                {
                    continue;
                }

                // blank line is an indication the definition has concluded
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lineBuilder.Append(line.Trim())
                        .Append(' ');

                    continue;
                }

                var fullLine = lineBuilder.ToString().Trim();

                var definitionLine = GetDefinitionLine(fullLine);

                // skip false cases, where the Word is empty
                if (!string.IsNullOrEmpty(definitionLine.Word))
                {
                    definitionLine = CheckAndHandleIdem(previousDefinition, definitionLine);

                    previousDefinition = definitionLine;

                    // handle entries that list multiple suffixes or variations of a word
                    // - "Ápe, _a bee._ Ápi, _bees._" // TODO?
                    // - "Apẻndi[o], Apẻnd[o], _downe-hanging._"
                    // - "Affluíre, ísc[o], ít[o], _to flow vnto. Also to abound in wealth._"
                    foreach (var variant in GetVariations(definitionLine.Word))
                    {
                        yield return definitionLine with
                        {
                            Word = variant
                        };
                    }
                }
                else
                {
                    // hit a case where no word is identified
                    // there are some expected instances of this
                    Debugger.Break();
                }

                lineBuilder.Clear();
                handlingDefinition = false;
            }

            if (lineBuilder.Length > 0)
            {
                yield return GetDefinitionLine(lineBuilder.ToString());
            }
        }

        internal static bool IsEndOfDefinitions(string line) =>
            line.Trim().Equals("FINIS.", StringComparison.OrdinalIgnoreCase);

        internal static bool IsPageHeading(string line) =>
            !string.IsNullOrWhiteSpace(line) && line.All(char.IsAsciiLetterUpper);

        // while most definitions have ", _" immediately following the word, there are several exceptions, eg.
        // - "Anphiscij _as_ Amphiscij."
        // - "Ánsia _as_ Ansietà."
        // - Prẻzzáre,_ as_ Prẻgiáre, _to bargane or make price for any thing._
        // - R[o]mpicóll[o],_ a breake-necke place, a downefal, a headlong
        //   precipice,_ A r[o] mpicóll[o], _headlong, rashly, desperately, in danger
        //   of breaking ones necke.Also a desperate, rash or heedlesse fellow._
        internal static bool ContainsDefinition(string line) =>
            line.Contains(" _") || line.Contains(",_");

        // False cases:
        // - _Note that wheresoeuer_ AV, _commeth before any vowell, the_ V _may 
        //   euer be written and pronounced double or single, as pleaseth the writer
        //   or speaker; for you may euer write and say_ Auuacciáre, Auuedére,
        //   Auual[o] ráre, Auuampáre, Auueníre, Auu[o]l[o] ntáre, Auuinacciáre, &c.
        // - _As for the words that are ioyned vnto_ Chè, _which are very many I
        //   refer the reader to my rules at the word_ Chè.
        // TODO: weird case: [er] is not covered in transcription notes.
        //   in scans of Florio it looks the same as is used for Rintẻrzáre, ie. Rintẻrzáta cárta
        // - Rint[er]rzáta cárta, _a bun-carde. Also a carde prickt or packt for aduantage._
        internal static WordDefinition GetDefinitionLine(string line)
        {
            int index = line.IndexOf("_");
            var wordDefintion = new WordDefinition(
                CleanHtmlItalics(line[..index].Trim(',', ' ')),
                line[index..].Trim());
            return wordDefintion with
            {
                ReferencedWords = GetReferencedWords(wordDefintion.Definition).ToArray()
            };

            // some lines haven't had the HTML italics converted to the []
            // transcribers used <i>ò</i> to denote the long o pronunciation in the html file, but [ò] for the same in the markdown
            static string CleanHtmlItalics(string word) =>
                word.Replace("<i>", "[").Replace("</i>", "]");
        }

        // TODO: consider adding the word matching to the previous definition as a referenced word
        internal static WordDefinition CheckAndHandleIdem(WordDefinition previousDefinition, WordDefinition definitionLine)
        {
            // handle "idem.", meaning "as above"
            // - may be
            //   - capitalised ("Idem."),
            //   - have "." on other side of markdown "_" (eg. "_Idem_.") or missing ("Idem, precisely")
            // - sometimes has extra after it (further definition, or referring to word)
            if (definitionLine.Definition.StartsWith("_idem", StringComparison.InvariantCultureIgnoreCase))
            {
                definitionLine = definitionLine with
                {
                    Definition = definitionLine.Definition
                        .Replace("_idem", previousDefinition.Definition)
                        .Replace("__", "_")
                        .Replace("._._", "._"),
                    ReferencedWords = [previousDefinition.Word]
                };
            }

            return definitionLine;
        }

        // handle "as X", referring to another definition
        // eg.
        // - "Abacáre, _as_ Abbacáre."
        // - "Abbẻlláre, _as_ Abbẻllíre, _also to sooth vp, or please ones mind._"
        internal static IEnumerable<string> GetReferencedWords(string definition)
        {
            // for loop variant is slightly better performing (both time and memory) than LINQ version
            // consider revisiting if a variant of this that better leverages SIMD is identified

            //return definition
            //    .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            //    .Where((_, idx) => idx % 2 == 1)
            //    .Select(s => s.Replace("&c", "").Trim([' ', ',', '.']));

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

        // handle entries that list multiple suffixes or variations of a word
        // - "Ápe, _a bee._ Ápi, _bees._" // TODO?
        // - "Apẻndi[o], Apẻnd[o]" => [ "Apẻndi[o]", "Apẻnd[o]" ]
        // - "Affluíre, ísc[o], ít[o]" => [ "Affluíre, Affluísc[o], Affluít[o]" ]
        // false cases:
        // - Dissegnáre, &c.
        // - Distrigáre, &c
        // - Fémmina, &c.
        // - Póca lána & gran r[o]m[ó]re
        // - Sẻttezz[ó]ni, p[ó]nti, c[o]lisẻi, acqued[ó]tti, & sẻttezz[ó]ni
        // - Torpẻnte, quási pígro, & oti[ó]s[o]
        internal static IEnumerable<string> GetVariations(string wordWithVariations)
        {
            if (wordWithVariations.Contains('&'))
            {
                return [wordWithVariations];
            }

            var tokens = wordWithVariations.Split(
                [",", "_or_"],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string originalWord = tokens[0];
            var variations = new List<string>()
            {
                originalWord
            };

            int originalWordOffset = -1;

            foreach (var variant in tokens[1..])
            {
                bool foundSuffixLocation = false;

                if (originalWordOffset != -1)
                {
                    variations.Add($"{originalWord[..originalWordOffset]}{variant}");
                    continue;
                }

                for (int i = variant.Length; ; i--)
                {
                    int index = originalWord.IndexOf(variant[..i]);
                    if (index > -1)
                    {
                        originalWordOffset = index;
                        foundSuffixLocation = true;
                        variations.Add($"{originalWord[..originalWordOffset]}{variant}");
                        break;
                    }
                }

                if (!foundSuffixLocation)
                {
                    variations.Add(variant);
                }
            }

            return variations;
        }
    }
}
