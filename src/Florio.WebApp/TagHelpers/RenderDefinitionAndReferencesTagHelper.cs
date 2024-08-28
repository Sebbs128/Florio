using System.Diagnostics;
using System.Text;

using Florio.Data;
using Florio.VectorEmbeddings.EmbeddingsModel;
using Florio.VectorEmbeddings.Repositories;
using Florio.WebApp.Extensions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Florio.WebApp.TagHelpers;

[HtmlTargetElement("render-definition", TagStructure = TagStructure.NormalOrSelfClosing)]
public class RenderDefinitionAndReferencesTagHelper(
    IStringFormatter stringFormatter,
    IVectorEmbeddingModelFactory embeddingsModelFactory,
    IWordDefinitionRepository repository,
    IUrlHelperFactory urlHelperFactory)
    : TagHelper
{
    private readonly IStringFormatter _stringFormatter = stringFormatter;
    private readonly VectorEmbeddingModel _embeddingsModel = embeddingsModelFactory.GetModel();
    private readonly IWordDefinitionRepository _repository = repository;
    private readonly IUrlHelperFactory _urlHelperFactory = urlHelperFactory;

    public WordDefinition WordDefinition { get; set; }

    [HtmlAttributeNotBound]
    [ViewContext]
    public ViewContext ViewContext { get; set; } = default!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var urlHelper = _urlHelperFactory.GetUrlHelper(ViewContext);

        output.TagName = "span";

        if (WordDefinition.Definition.Contains('}'))
        {
            await WriteDefinitionWithTable(urlHelper, output);
            return;
        }

        var splitDefinition = WordDefinition.Definition
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select((s, i) => (Text: s, Index: i));

        var lastFoundRefWord = -1;
        foreach (var item in splitDefinition)
        {
            if (item.Index % 2 == 0)
            {
                WriteDefinitionText(item.Text, output);
                continue;
            }

            if (WordDefinition.ReferencedWords is null or { Length: 0 })
            {
                // TODO: check if anything hits here where item.Text is whitespace. Could possibly trim split entries
                Debugger.Break();
                continue;
            }

            var searchStartIndex = 0;
            for (var i = lastFoundRefWord + 1; i < WordDefinition.ReferencedWords!.Length; i++)
            {
                var refWord = WordDefinition.ReferencedWords[i];
                var index = item.Text.IndexOf(refWord, searchStartIndex);

                if (index == -1)
                {
                    break;
                }

                lastFoundRefWord = i;
                await WriteReferenceLink(item.Text[searchStartIndex..(index + refWord.Length)],
                    refWord,
                    index - searchStartIndex,
                    urlHelper,
                    output);
                searchStartIndex = index + refWord.Length;
            }

            if (searchStartIndex <= item.Text.Length - 1)
            {
                output.Content.Append(item.Text[searchStartIndex..]);
            }
        }
    }

    private async Task WriteDefinitionWithTable(IUrlHelper urlHelper, TagHelperOutput output)
    {
        var endDefinition = WordDefinition.Definition.IndexOf('_', 1);
        WriteDefinitionText(WordDefinition.Definition[1..endDefinition], output);

        var lines = WordDefinition.Definition[(endDefinition + 1)..]
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.Equals(line, "}", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var rightContent = string.Join(string.Empty, lines.Select(line => line[(line.IndexOf('}') + 1)..])).Trim([' ', '_']);

        var table = new TagBuilder("table");
        var tbody = new TagBuilder("tbody");
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var tr = new TagBuilder("tr");

            var td = new TagBuilder("td");
            td.AddCssClass("text-nowrap");
            var refWord = WordDefinition.ReferencedWords!.Single(line.StartsWith);
            td.InnerHtml.AppendHtml(await BuildReferenceLink(refWord, urlHelper));
            td.InnerHtml.Append(line[refWord.Length..line.IndexOf('}')]);

            tr.InnerHtml.AppendHtml(td);

            if (i == 0)
            {
                var middleTd = new TagBuilder("td");
                middleTd.MergeAttribute("rowspan", lines.Length.ToString());

                var rightBraceImg = new TagBuilder("img");
                rightBraceImg.MergeAttribute("src", urlHelper.Content("~/images/right-brace.png"));

                middleTd.InnerHtml.AppendHtml(rightBraceImg);
                tr.InnerHtml.AppendHtml(middleTd);

                var rightTd = new TagBuilder("td");
                rightTd.MergeAttribute("rowspan", lines.Length.ToString());
                rightTd.AddCssClass("align-middle");

                var italics = new TagBuilder("em");
                WriteIntoTagBuilder(rightContent, italics);
                rightTd.InnerHtml.AppendHtml(italics);
                tr.InnerHtml.AppendHtml(rightTd);
            }

            tbody.InnerHtml.AppendHtml(tr);
        }
        table.InnerHtml.AppendHtml(tbody);
        output.Content.AppendHtml(table);
    }

    private static void WriteDefinitionText(string text, TagHelperOutput output)
    {
        var italics = new TagBuilder("em");

        WriteIntoTagBuilder(text, italics);

        output.Content.AppendHtml(italics);
    }

    private async Task WriteReferenceLink(string text, string referencedWord, int startIndex, IUrlHelper urlHelper, TagHelperOutput output)
    {
        output.Content.AppendHtml(text[..startIndex].Replace("\r\n", "<br />"));

        var linkTag = await BuildReferenceLink(referencedWord, urlHelper);

        output.Content.AppendHtml(linkTag);
    }

    private async Task<TagBuilder> BuildReferenceLink(string referencedWord, IUrlHelper urlHelper)
    {
        var linkTag = new TagBuilder("a");

        var normalised = _stringFormatter.ToPrintableNormalizedString(referencedWord);
        var vector = _embeddingsModel.CalculateVector(_stringFormatter.NormalizeForVector(normalised));
        var matches = _repository.FindClosestMatch(vector);

        if (await matches.AnyAsync())
        {
            linkTag.MergeAttribute("href", urlHelper.Page("Italian", new { word = normalised }));
            linkTag.MergeAttribute("data-bs-container", "body");
            linkTag.MergeAttribute("data-bs-toggle", "popover");
            linkTag.MergeAttribute("data-bs-placement", "bottom");
            linkTag.MergeAttribute("data-bs-content", await BuildPopoverContent(matches));
        }
        else
        {
            linkTag.MergeAttribute("href", urlHelper.Page("Search", new { Term = normalised }));
        }

        WriteIntoTagBuilder(referencedWord.ToHtmlString().Value!, linkTag);
        return linkTag;
    }

    private static async Task<string> BuildPopoverContent(IAsyncEnumerable<WordDefinition> matches)
    {
        var first = await matches.FirstAsync();
        var numberRemaining = await matches.CountAsync() - 1;

        var content = new StringBuilder();

        var italicsOpen = false;
        var prevIndex = 0;
        // TODO: truncate?
        var toFormat = first.Definition.ToHtmlString().Value!;
        int index;
        while ((index = toFormat.IndexOf('_', prevIndex)) > -1)
        {
            content.Append(toFormat[prevIndex..index])
                .Append(!italicsOpen ? "<i>" : "</i>");
            italicsOpen = !italicsOpen;
            prevIndex = index + 1;
        }
        content.Append(toFormat[prevIndex..]);
        if (italicsOpen)
        {
            content.Append("</i>");
        }

        if (numberRemaining > 0)
        {
            content.Append($"<br />...and {numberRemaining} more.");
        }

        return content.ToString();
    }

    private static void WriteIntoTagBuilder(string text, TagBuilder tagBuilder)
    {

        int startSuperscript;
        while ((startSuperscript = text.IndexOf('^')) != -1)
        {
            tagBuilder.InnerHtml.AppendHtml(text[..startSuperscript]);

            var endSuperscript = text.IndexOfAny([' ', ',', '.'], startSuperscript);
            var superscript = new TagBuilder("sup");
            superscript.InnerHtml.Append(text[(startSuperscript + 1)..endSuperscript]);

            tagBuilder.InnerHtml.AppendHtml(superscript);
            text = text[endSuperscript..];
        }

        tagBuilder.InnerHtml.AppendHtml(text);
    }
}
