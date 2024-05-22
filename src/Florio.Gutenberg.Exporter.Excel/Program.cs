using System.Data;

using ClosedXML.Excel;

using Florio.Gutenberg.Parser;

using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection()
    .AddSingleton<IGutenbergTextDownloader, GutenbergTextDownloader>()
    .AddSingleton<GutenbergTextParser>();

services.AddHttpClient<GutenbergTextDownloader>();
var serviceProvider = services.BuildServiceProvider();

var parser = serviceProvider.GetRequiredService<GutenbergTextParser>();

var byFirstLetter = await parser.ParseLines()
    .ToLookupAsync(wd => StringUtilities.Normalize(wd.Word).First());

using var excelFile = new XLWorkbook();

var titleWorksheet = excelFile.AddWorksheet("Title");

titleWorksheet.Cell("A1")
    .CreateRichText()
    .AddText(Constants.Gutenberg_Attribution);

titleWorksheet.Cell("A3")
    .CreateRichText()
    .AddText("Florio's 1611 Italian/English Dictionary: Queen Anna's New World of Words")
    .SetBold(true);

foreach (var letterGroup in byFirstLetter.OrderBy(l => l.Key))
{
    var dataTable = new DataTable();
    dataTable.Columns.Add(nameof(WordDefinition.Word));
    dataTable.Columns.Add(nameof(WordDefinition.Definition));
    dataTable.Columns.Add(nameof(WordDefinition.ReferencedWords));

    dataTable.Rows.Add("Word", "Definition", "Phrases or Referenced Words");

    foreach (var wordDefinition in letterGroup)
    {
        dataTable.Rows.Add(
            StringUtilities.GetPrintableString(wordDefinition.Word),
            wordDefinition.Definition,
            string.Join('\n', wordDefinition.ReferencedWords ?? Enumerable.Empty<string>()));
    }

    var worksheet = excelFile.AddWorksheet(char.ToUpperInvariant(letterGroup.Key).ToString());
    worksheet.Cell("A1").InsertData(dataTable);
}

excelFile.SaveAs("Florio Italian-English.xlsx");