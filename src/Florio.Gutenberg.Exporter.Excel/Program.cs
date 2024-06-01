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
    .ToLookupAsync(wd => StringUtilities.GetPrintableNormalizedString(wd.Word).First());

using var excelFile = new XLWorkbook();

var titleWorksheet = excelFile.AddWorksheet("Title");

var gutenbergLicenseCell = titleWorksheet.Cell("A1");
gutenbergLicenseCell.CreateRichText()
    .AddText(Constants.Gutenberg_Attribution);
gutenbergLicenseCell.Style.Alignment.WrapText = true;

var titleCell = titleWorksheet.Cell("A3");
titleCell.Style.Font.FontSize = 20;
titleCell.Style.Font.Bold = true;
titleCell.CreateRichText()
    .AddText("Florio's 1611 Italian/English Dictionary:")
    .AddNewLine()
    .AddText("Queen Anna's New World of Words");
titleCell.Style.Alignment.WrapText = true;
titleCell.WorksheetColumn().AdjustToContents();

var notesCell = titleWorksheet.Cell("A6");
notesCell.CreateRichText()
    .AddText(Constants.Gutenberg_Transcribers_Note);
notesCell.Style.Alignment.WrapText = true;

foreach (var letterGroup in byFirstLetter.OrderBy(l => l.Key))
{
    var worksheet = excelFile.AddWorksheet(char.ToUpperInvariant(letterGroup.Key).ToString());

    var row = worksheet.Row(1);
    row.Cell(1).CreateRichText().AddText("Word").SetBold();
    row.Cell(2).CreateRichText().AddText("Definition").SetBold();
    row.Cell(3).CreateRichText().AddText("Phrases or Referenced Words").SetBold();

    foreach (var wordDefinition in letterGroup)
    {
        row = row.RowBelow();
        row.Cell(1).CreateRichText().AddText(wordDefinition.Word);

        var definitionCellRichText = row.Cell(2).CreateRichText();
        var parts = wordDefinition.Definition.Split('_', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            definitionCellRichText.AddText(parts[i]).SetItalic(i % 2 == 0);
        }

        row.Cell(3).CreateRichText()
            .AddText(string.Join('\n', wordDefinition.ReferencedWords ?? Enumerable.Empty<string>()));
    }

    //worksheet.Cell("A1").InsertData(dataTable);

    worksheet.Column(1).AdjustToContents();

    worksheet.Column(2).Style.Alignment.WrapText = true;
    worksheet.Column(2).AdjustToContents();

    worksheet.Column(3).Style.Alignment.WrapText = true;
    worksheet.Column(3).AdjustToContents();
}

excelFile.SaveAs("Florio Italian-English.xlsx");