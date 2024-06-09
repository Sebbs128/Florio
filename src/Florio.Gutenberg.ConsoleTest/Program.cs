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

Console.WriteLine($"{wordDefinitions.Count} words were parsed.");
Console.WriteLine();

var longestWord = wordDefinitions.MaxBy(wd => wd.Word.Length);
var longestDefinition = wordDefinitions.MaxBy(wd => wd.Definition.Length);

Console.WriteLine($"Longest word ({longestWord.Word.Length} chars): {longestWord.Word}");
Console.WriteLine();
Console.WriteLine($"Longest definition ({longestDefinition.Definition.Length} chars): {longestDefinition.Definition}");
Console.WriteLine();

var potentialWordVariationIssues = wordDefinitions
    .Select(wd => wd.Word)
    .Where(w => char.IsLower(w, 0))
    .ToList();

Console.WriteLine($"Words that appear to be incorrectly parsed word variations:");
Console.WriteLine(string.Join("\n", potentialWordVariationIssues));
Console.WriteLine();

var containingBrackets = wordDefinitions
    .Select(wd => stringFormatter.ToPrintableNormalizedString(wd.Word))
    .Where(w => w.IndexOfAny(['[', ']']) > 0)
    .ToList();

if (containingBrackets.Count != 0)
    Debugger.Break();

var charsInAllWords = wordDefinitions
    .SelectMany(wd => stringFormatter.ToPrintableNormalizedString(wd.Word))
    .Distinct()
    .Order()
    .Select(c => $"'{c}'")
    .ToArray();

Console.WriteLine($"Characters used in words: {string.Join(", ", charsInAllWords)}");