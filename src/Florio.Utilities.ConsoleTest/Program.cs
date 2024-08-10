using System.Diagnostics;
using System.Text;

using Florio.Data;

using Microsoft.Extensions.DependencyInjection;

Console.OutputEncoding = Encoding.UTF8;

var services = new ServiceCollection()
    .AddGutenbergDownloaderAndParser(@".localassets\pg56200.txt");

var serviceProvider = services.BuildServiceProvider();

var stringFormatter = serviceProvider.GetRequiredService<IStringFormatter>();
var parser = serviceProvider.GetRequiredService<IWordDefinitionParser>();
var wordDefinitions = await parser.ParseLines().ToListAsync();

PrintTotalCount(wordDefinitions);

PrintMaxLengths(wordDefinitions);

PrintAnyPossibleVariationIssues(wordDefinitions);

CheckForMissedSquareBrackets(stringFormatter, wordDefinitions);

PrintCharactersFoundInWords(stringFormatter, wordDefinitions);

PrintCharactersUsedForVectors(stringFormatter, wordDefinitions);

static void PrintTotalCount(List<WordDefinition> wordDefinitions)
{
    Console.WriteLine($"{wordDefinitions.Count} words were parsed.");
    Console.WriteLine();
}

static void PrintMaxLengths(List<WordDefinition> wordDefinitions)
{
    var longestWord = wordDefinitions.MaxBy(wd => wd.Word.Length);
    var longestDefinition = wordDefinitions.MaxBy(wd => wd.Definition.Length);

    Console.WriteLine($"Longest word ({longestWord.Word.Length} chars): {longestWord.Word}");
    Console.WriteLine();
    Console.WriteLine($"Longest definition ({longestDefinition.Definition.Length} chars): {longestDefinition.Definition}");
    Console.WriteLine();
}

static void PrintAnyPossibleVariationIssues(List<WordDefinition> wordDefinitions)
{
    var potentialWordVariationIssues = wordDefinitions
        .Select(wd => wd.Word)
        .Where(w => char.IsLower(w, 0))
        .ToList();

    if (potentialWordVariationIssues.Count > 0)
    {
        Console.WriteLine($"Words that appear to be incorrectly parsed word variations:");
        Console.WriteLine(string.Join("\n", potentialWordVariationIssues));
    }
    else
    {
        Console.WriteLine("No potential incorrectly parsed word variations identitified.");
    }
    Console.WriteLine();
}

static void CheckForMissedSquareBrackets(IStringFormatter stringFormatter, List<WordDefinition> wordDefinitions)
{
    var containingBrackets = wordDefinitions
        .Select(wd => stringFormatter.ToPrintableNormalizedString(wd.Word))
        .Where(w => w.IndexOfAny(['[', ']']) > 0)
        .ToList();

    if (containingBrackets.Count != 0)
    {
        Debugger.Break();
    }
}

static void PrintCharactersFoundInWords(IStringFormatter stringFormatter, List<WordDefinition> wordDefinitions)
{
    var charsInAllWords = wordDefinitions
        .SelectMany(wd => stringFormatter.ToPrintableNormalizedString(wd.Word))
        .Distinct()
        .Order()
        .Select(c => $"'{c}'")
        .ToArray();

    Console.WriteLine($"Characters used in words: {string.Join(", ", charsInAllWords)}");
    Console.WriteLine();

    char[] unexpectedCharacters = ['&', ',', '-', '?'];
    foreach (char c in unexpectedCharacters)
    {
        var words = wordDefinitions
            .Select(wd => stringFormatter.ToPrintableNormalizedString(wd.Word))
            .Where(w => w.Contains(c));

        Console.WriteLine($"Words containing '{c}':");
        Console.WriteLine(string.Join('\n', words));
        Console.WriteLine();
    }
}

static void PrintCharactersUsedForVectors(IStringFormatter stringFormatter, List<WordDefinition> wordDefinitions)
{
    var charsInAllWords = wordDefinitions
        .SelectMany(wd => stringFormatter.NormalizeForVector(wd.Word))
        .Distinct()
        .Order()
        .Select(c => $"'{c}'")
        .ToArray();

    Console.WriteLine($"Characters used in words: {string.Join(", ", charsInAllWords)}");
    Console.WriteLine($"({charsInAllWords.Length} total)");
    Console.WriteLine();
}