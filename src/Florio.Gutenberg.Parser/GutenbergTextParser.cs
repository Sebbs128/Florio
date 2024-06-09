﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

using Florio.Data;

namespace Florio.Gutenberg.Parser;

public class GutenbergTextParser(IGutenbergTextDownloader downloader) : IWordDefinitionParser
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
                if (line.Contains('}', StringComparison.OrdinalIgnoreCase))
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
            definitionLine = CheckAndHandleIdem(definitionLine, state.PreviousDefinition);

            state.UpdatePreviousDefinition(definitionLine);

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
        line.Trim().Equals("FINIS.", StringComparison.Ordinal);

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
    // Vliuígn[o] has been fixed in PG as of 2024-05-30
    internal static bool ContainsDefinition(string line) =>
        line.Contains(" _", StringComparison.OrdinalIgnoreCase) || line.Contains(",_", StringComparison.OrdinalIgnoreCase) ||
        // special edge cases
        line.StartsWith("Fáre a guísa délla", StringComparison.Ordinal) ||
        line.StartsWith("Vliuígn[o]", StringComparison.Ordinal); // this has been fixed in PG as of 2024-05-30

    // False cases:
    // - _Note that wheresoeuer_ AV, _commeth before any vowell, the_ V _may 
    //   euer be written and pronounced double or single, as pleaseth the writer
    //   or speaker; for you may euer write and say_ Auuacciáre, Auuedére,
    //   Auual[o] ráre, Auuampáre, Auueníre, Auu[o]l[o] ntáre, Auuinacciáre, &c.
    // - _As for the words that are ioyned vnto_ Chè, _which are very many I
    //   refer the reader to my rules at the word_ Chè.
    internal static WordDefinition GetDefinitionLine(string line)
    {
        int index;
        // special-case the Vliuígn[o] edge-case
        // fixed in PG as of 2024-05-30
        if (line.StartsWith("Vliuígn[o]", StringComparison.Ordinal) && !line.Contains('_', StringComparison.OrdinalIgnoreCase))
        {
            index = line.IndexOf(',', StringComparison.OrdinalIgnoreCase);
        }
        // special-case edge cases like "Ẻssere, s[o]n[o], fui, f[ó]ra, stát[o] _or_ sút[o]"
        else if ((uint)line.IndexOf(" _or_ ", StringComparison.Ordinal) < line.IndexOf(", _", StringComparison.OrdinalIgnoreCase))
        {
            // special case "Fátt[o] _or_ fátta, _following_ Sì, _or_ C[o]sì, _serueth for such, so made, or of such quality._"
            index = line.StartsWith("Fátt[o]", StringComparison.InvariantCulture)
                ? line.IndexOf(", _", StringComparison.OrdinalIgnoreCase)
                : line.IndexOf(", _", line.LastIndexOf("_or_", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            index = line.IndexOf('_');
        }

        var wordDefintion = new WordDefinition(
            Word: CleanInconsistencies(line[..index].Trim(',', ' ', ';')),
            Definition: line[index..].Trim(',', ' '));

        return wordDefintion with
        {
            ReferencedWords = GetReferencedWords(wordDefintion.Definition).Distinct().ToArray()
        };

        // some lines haven't had the HTML italics converted to the []
        //   transcribers used <i>ò</i> to denote the long o pronunciation in the html file, but [ò] for the same in the markdown
        // weird case: [er] is not covered in transcription notes.
        //   in scans of Florio it looks the same as is used for Rintẻrzáre, ie. Rintẻrzáta cárta
        //   - Rint[er]rzáta cárta, _a bun-carde. Also a carde prickt or packt for aduantage._
        // these have been fixed in PG as of 2024-05-30
        static string CleanInconsistencies(string word) => word
            .Replace("<i>", "[", StringComparison.Ordinal).Replace("</i>", "]", StringComparison.Ordinal)
            .Replace("[er]", "er", StringComparison.Ordinal);
    }

    // TODO: consider adding the word matching to the previous definition as a referenced word?
    //       - might be very difficult/impossible as WordDefinition is immutable, and
    //         ReferencedWords is an array
    internal static WordDefinition CheckAndHandleIdem(WordDefinition definitionLine, WordDefinition previousDefinition)
    {
        // handle "idem.", meaning "as above"
        // - may be
        //   - capitalised ("Idem."),
        //   - have "." on other side of markdown "_" (eg. "_Idem_.") or missing ("Idem, precisely")
        // - sometimes has extra after it (further definition, or referring to word)
        if (definitionLine.Definition.StartsWith("_idem", StringComparison.InvariantCulture))
        {
            definitionLine = definitionLine with
            {
                Definition = definitionLine.Definition
                    .Replace("_idem", previousDefinition.Definition, StringComparison.InvariantCulture)
                    .Replace("__", "_", StringComparison.OrdinalIgnoreCase)
                    .Replace("._._", "._", StringComparison.OrdinalIgnoreCase),
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

        var parts = definition.Split('_',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 1; i < parts.Length; i += 2)
        {
            if (parts[i].StartsWith("a, b, c", StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            if (parts[i].IndexOf(',') > 0 && parts[i] is not [.., ','])
            {
                Debugger.Break();
            }

            yield return parts[i]
                .Replace("&c", "", StringComparison.OrdinalIgnoreCase)
                .Trim([' ', ',', ':', ';', '.']);
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
        static bool IsSpecialEdgeCase(string word) =>
            word.Equals("Fattaménte, si", StringComparison.InvariantCulture) || // this is a phrase
            word.Equals("Marẻa, the", StringComparison.InvariantCulture) || // this is a transcription error
            word.Equals("Méschi[o],ed", StringComparison.InvariantCulture); // this is a transcription error

        if (wordWithVariations.Contains('&', StringComparison.OrdinalIgnoreCase))
        {
            return [wordWithVariations];
        }

        // handle special edge-cases
        if (IsSpecialEdgeCase(wordWithVariations))
        {
            return [wordWithVariations];
        }

        var tokens = wordWithVariations.Split(
            [",", "_or_", " or "],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Any(t => t.IndexOfAny([' ', '.']) > -1))
        {
            return [wordWithVariations];
        }

        var originalWord = tokens[0];
        var variations = new List<string>
        {
            originalWord
        };
        var originalLowered = originalWord.ToLowerInvariant();

        foreach (var variant in tokens[1..])
        {
            // if variant starts with uppercase, immediately add it
            //   eg. "Affra, Afra" => [ "Affra", "Afra" ]
            if (char.IsUpper(variant[0]))
            {
                variations.Add(variant);
                continue;
            }

            var variantLowered = variant.ToLowerInvariant();
            // constrain the search range
            var charVariants = GetCharacterVariants(variantLowered[0]);
            var startOfRange = originalLowered.IndexOfAny(charVariants);
            var endOfRange = originalLowered.LastIndexOfAny(charVariants);

            if (startOfRange == endOfRange)
            {
                if (startOfRange == -1)
                {
                    // handle special cases
                    // - "S[o]praẻssere, s[o]pras[ó]n[o], fúi, stát[o]"
                    // - "S[o]prandáre, uád[o], andái, andát[o]"
                    // Sopraẻssere (et al) are conjunctions. eg. Sopra ẻssere
                    // "S[o]prauád[o]" looks odd but is correct, because "uád[o]" is "vàdo"
                    if (wordWithVariations.StartsWith("S[o]pra", StringComparison.InvariantCulture))
                    {
                        var withoutSopra = wordWithVariations.Replace("s[o]pra", "", StringComparison.InvariantCultureIgnoreCase);
                        var newVariants = GetVariations(withoutSopra);
                        return newVariants.Select(w => $"S[o]pra{w.ToLowerInvariant().Trim('a')}");
                    }

                    return tokens.Select(w =>
                    {
                        // if starts with "u", change to "V"
                        // don't need to worry about diacritics (eg. 'Ú') here because no cases hit this branch
                        var firstLetter = char.ToUpperInvariant(w[0] == 'U'
                            ? 'V'
                            : w[0]);

                        return $"{firstLetter}{w[1..]}";
                    });
                }
                if (startOfRange == 0)
                {
                    if (originalWord.StartsWith("V´", StringComparison.InvariantCulture))
                    {
                        variations.Add($"{originalWord[..2]}{variant[1..]}");
                        continue;
                    }

                    variations.Add($"{originalWord[..1]}{variant[1..]}");
                    continue;
                }

                variations.Add($"{originalWord[..startOfRange]}{variant}");
                continue;
            }

            // if the range is shorter than the variant length, we can immediately cut down our search
            var searchLength = Math.Min(variant.Length, endOfRange - startOfRange);

            var searchSpan = originalLowered.Substring(startOfRange, searchLength);

            for (var z = searchLength; z > 0; z--)
            {
                var index = originalLowered.LastIndexOf(variantLowered[..z], StringComparison.InvariantCulture);

                if (index == 0)
                {
                    variations.Add($"{originalWord[..1]}{variant[1..]}");
                    break;
                }
                if (index > -1)
                {
                    variations.Add($"{originalWord[..index]}{variant}");
                    break;
                }
            }
        }

        return variations;
    }

    internal static char[] GetCharacterVariants(char c)
    {
        var charVariants = new List<char> { c };
        var normalizedChar = c.ToString().Normalize(NormalizationForm.FormD);

        if (normalizedChar[0] is 'u')
        {
            charVariants.Add('v');
            if (normalizedChar.Length == 1)
            {
                charVariants.Add('ú');
            }
        }
        if (normalizedChar[0] is 'v')
        {
            charVariants.Add('u');
        }
        if (normalizedChar.Length > 1)
        {
            charVariants.Add(normalizedChar[0]);
        }

        return [.. charVariants];
    }
}
