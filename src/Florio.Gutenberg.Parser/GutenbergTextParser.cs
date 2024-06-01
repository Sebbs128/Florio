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

            var state = new ParserState();
            var lineEnumerator = _downloader.ReadLines(cancellationToken).GetAsyncEnumerator(cancellationToken);

            while (await lineEnumerator.MoveNextAsync())
            {
                var line = lineEnumerator.Current;

                // handle end indicator
                // end indicator is "                                 FINIS."
                if (IsEndOfDefinitions(line))
                {
                    break;
                }

                // skip lines until we reach actual definitions ie. being after a heading such as "A"
                if (!state.HaveReachedEntries)
                {
                    if (IsPageHeading(line))
                    {
                        state.ReachedEntries();
                    }
                    continue;
                }

                //  ignore page headers ("A", "ABB" etc. anything not containing ", _")
                // they indicate that we've reach the section containing definitions though
                if (!state.CurrentlyHandlingDefinition && ContainsDefinition(line))
                {
                    state.HandlingDefinition();
                }

                if (!state.CurrentlyHandlingDefinition)
                {
                    continue;
                }

                // blank line is an indication the definition has concluded
                // because of an edge case containing several examples (lines containing a '}')
                //  we need to read the line after a blank line to determine if the
                //  currently built line should be returned or not
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (await lineEnumerator.MoveNextAsync())
                    {
                        line = lineEnumerator.Current;

                        if (!line.Contains('}'))
                        {
                            foreach (var definitionLine in ParseBuiltLine(lineBuilder, state))
                            {
                                yield return definitionLine;
                            }

                            if (!ContainsDefinition(line))
                            {
                                state.FinishedHandlingDefintion();
                                continue;
                            }
                        }
                        else
                        {
                            // add a blank line if the current line is part of an expanded definition
                            // (first is merely ending the previous line; second is the blank line)
                            lineBuilder.AppendLine()
                                .AppendLine();
                        }
                    }
                }

                if (!string.IsNullOrEmpty(line))
                {
                    if (line.Contains('}'))
                    {
                        lineBuilder.AppendLine(line);
                    }
                    else
                    {
                        lineBuilder.Append(' ')
                            .Append(line.Trim());
                    }
                }
            }

            // handle anything that was left after reaching the end.
            if (lineBuilder.Length > 0)
            {
                foreach (var definitionLine in ParseBuiltLine(lineBuilder, state))
                {
                    yield return definitionLine;
                }
            }
        }

        internal static IEnumerable<WordDefinition> ParseBuiltLine(StringBuilder lineBuilder, ParserState state)
        {
            var fullLine = lineBuilder.ToString().Trim();
            lineBuilder.Clear();

            var definitionLine = GetDefinitionLine(fullLine);

            // skip false cases, where the Word is empty
            if (!string.IsNullOrEmpty(definitionLine.Word))
            {
                definitionLine = CheckAndHandleIdem(state.PreviousDefinition, definitionLine);

                state.UpdatePreviousDefinition(definitionLine);

                //// TODO: fix variation edge cases
                //yield return definitionLine;
                //yield break;
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
                //Debugger.Break();
            }
        }

        internal class ParserState
        {
            public bool HaveReachedEntries { get; private set; } = false;
            public bool CurrentlyHandlingDefinition { get; private set; } = false;

            public WordDefinition PreviousDefinition { get; private set; } = default;

            public void ReachedEntries()
            {
                HaveReachedEntries = true;
            }

            public void HandlingDefinition()
            {
                CurrentlyHandlingDefinition = true;
            }

            public void FinishedHandlingDefintion()
            {
                CurrentlyHandlingDefinition = false;
            }

            public void UpdatePreviousDefinition(WordDefinition newPreviousDefinition)
            {
                PreviousDefinition = new()
                {
                    Word = newPreviousDefinition.Word,
                    Definition = newPreviousDefinition.Definition,
                    ReferencedWords = newPreviousDefinition.ReferencedWords,
                };
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
        // - Vliuígn[o], of forme or colour of an oliue.
        // - Fáre a guísa délla c[ó]da del pórc[o] che tútt[o] il gi[ó]rn[o] se la
        //   diména, e pói la séra n[o] n hà fátt[o] núlla, _to doe as the hog doth
        //   that all day wags his taile and at night hath done nothing, much adoe
        //   and neuer the neerer, doe and vndoe the day is long enough._
        internal static bool ContainsDefinition(string line) =>
            line.Contains(" _") || line.Contains(",_") ||
            // special edge cases
            line.StartsWith("Fáre a guísa délla", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("Vliuígn[o]", StringComparison.OrdinalIgnoreCase); // this has been fixed in PG as of 2024-05-30

        // False cases:
        // - _Note that wheresoeuer_ AV, _commeth before any vowell, the_ V _may 
        //   euer be written and pronounced double or single, as pleaseth the writer
        //   or speaker; for you may euer write and say_ Auuacciáre, Auuedére,
        //   Auual[o] ráre, Auuampáre, Auueníre, Auu[o]l[o] ntáre, Auuinacciáre, &c.
        // - _As for the words that are ioyned vnto_ Chè, _which are very many I
        //   refer the reader to my rules at the word_ Chè.
        internal static WordDefinition GetDefinitionLine(string line)
        {
            char startOfDefinition = '_';
            // special-case the Vliuígn[o] edge-case
            if (line.StartsWith("Vliuígn[o]", StringComparison.OrdinalIgnoreCase) && !line.Contains('_'))
            {
                startOfDefinition = ',';
            }

            int index = line.IndexOf(startOfDefinition);
            var wordDefintion = new WordDefinition(
                Word: CleanInconsistencies(line[..index].Trim(',', ' ')),
                Definition: line[index..].Trim(',', ' '));

            return wordDefintion with
            {
                ReferencedWords = GetReferencedWords(wordDefintion.Definition).ToArray()
            };

            // some lines haven't had the HTML italics converted to the []
            //   transcribers used <i>ò</i> to denote the long o pronunciation in the html file, but [ò] for the same in the markdown
            // weird case: [er] is not covered in transcription notes.
            //   in scans of Florio it looks the same as is used for Rintẻrzáre, ie. Rintẻrzáta cárta
            //   - Rint[er]rzáta cárta, _a bun-carde. Also a carde prickt or packt for aduantage._
            // these has been fixed in PG as of 2024-05-30
            static string CleanInconsistencies(string word) => word
                .Replace("<i>", "[").Replace("</i>", "]")
                .Replace("[er]", "er");
        }

        // TODO: consider adding the word matching to the previous definition as a referenced word?
        //       - might be very difficult/impossible as WordDefinition is immutable, and
        //         ReferencedWords is an array
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
                    ReferencedWords = previousDefinition.ReferencedWords is null
                        ? [previousDefinition.Word]
                        : Enumerable.Union(previousDefinition.ReferencedWords, [previousDefinition.Word]).ToArray()
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
            //    .Select(s => s.Replace("&c", "").Trim([' ', ',', ':', '.']));

            var parts = definition.Split(
                '_',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (int i = 1; i < parts.Length; i += 2)
            {
                yield return parts[i]
                    .Replace("&c", "")
                    .Trim([' ', ',', ':', '.']);
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
                [",", "_or_", " or "],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // causes " or " to not be handled
            // eg. "Auedére, véd[o] or végg[o], víddi, vedút[o] or víst[o], _to perceiue or be aware, to aduise, or foresee._"
            // could possibly move " or " to the Split() above
            if (tokens.Any(t => t.Contains(' ') || t.Contains('.')))
            {
                return [wordWithVariations];
            }

            string originalWord = tokens[0];
            var variations = new List<string>
            {
                originalWord
            };

            int originalWordOffset = -1;

            foreach (var variant in tokens[1..])
            {
                // notes for improving
                // current loop trims off end until it finds a match.
                //   could maybe enclose it in a loop that trims the start to help with preserving original (uppercase) characters?
                //   second, third etc iterations of outer loop would only occur if index was <= 0
                // check if variant[0] == originalWord[originalWordOffset] before using?
                // support interchangable letters... eg 'u' and 'v'
                //   "Accẻruíre, vísc[o], vít[o]" would be {Accẻruíre, Accẻruísc[o], Accẻruít[o]}
                //   this might be something adding an outloop solves?
                //   IndexOfAny() doesn't support strings, so using it for this case would require either a different path or a rewrite of the loop
                //     eg. find first letter (and valid replacements eg 'u'<==>'v'), then check remainder of variant
                // what should "Andáre, vád[o], andái, andát[o]" be?
                // what should "Dẻ[o], as Dí[o], _GOD._" be? looks like it should have been transcibed as "Dẻ[o], _as_ Dí[o], _GOD._" (https://www.pbm.com/~lindahl/florio/156.html)

                // if variant starts with uppercase, immediately add it? the index-matching loop would be unnecessary
                //   eg. "Accẻndere, accẻnd[o], accési, accés[o]" should be {Accẻndere, Accẻnd[o], Accési, Accés[o]}
                if (char.IsUpper(variant[0]))
                {
                    variations.Add(variant);
                    continue;
                }

                bool foundSuffixLocation = false;

                if (originalWordOffset > 0)
                {
                    variations.Add($"{originalWord[..originalWordOffset]}{variant}");
                    continue;
                }

                // moves through the variant from the end
                for (int i = variant.Length; ; i--)
                {
                    int index = originalWord.IndexOf(variant[1..i]);
                    if (index == 0)
                    {
                        Debugger.Break();
                    }
                    if (index > -1)
                    {
                        // in most cases the variations are from the same location in the original word
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
