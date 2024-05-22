using System.Diagnostics;

using Florio.Gutenberg.Parser;

using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection()
    .AddSingleton<IGutenbergTextDownloader, GutenbergTextDownloader>()
    .AddSingleton<GutenbergTextParser>();

services.AddHttpClient<GutenbergTextDownloader>();
var serviceProvider = services.BuildServiceProvider();

var parser = serviceProvider.GetRequiredService<GutenbergTextParser>();
var wordDefinitions = await parser.ParseLines().ToListAsync();

Console.WriteLine($"{wordDefinitions.Count} words were parsed.");

var longestWord = wordDefinitions.MaxBy(wd => wd.Word.Length);
var longestDefinition = wordDefinitions.MaxBy(wd => wd.Definition.Length);

Console.WriteLine($"Longest word ({longestWord.Word.Length} chars): {longestWord.Word}");
Console.WriteLine($"Longest definition ({longestDefinition.Definition.Length} chars): {longestDefinition.Definition}");

var containingBrackets = wordDefinitions
    .Select(wd => StringUtilities.Normalize(wd.Word))
    .Where(w => w.IndexOfAny(['[', ']']) > 0)
    .ToList();

if (containingBrackets.Count != 0)
    Debugger.Break();

var charsInAllWords = wordDefinitions
    .SelectMany(wd => StringUtilities.Normalize(wd.Word))
    .Distinct()
    .Order()
    .Select(c => $"'{c}'")
    .ToArray();

Console.WriteLine($"Characters used in words: {string.Join(", ", charsInAllWords)}");