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

    private static void WriteDefinitionText(string text, TagHelperOutput output)
    {
        var italics = new TagBuilder("i");
        italics.InnerHtml.Append(text);

        output.Content.AppendHtml(italics);
    }

    private async Task WriteReferenceLink(string text, string referencedWord, int startIndex, IUrlHelper urlHelper, TagHelperOutput output)
    {
        output.Content.AppendHtml(text[..startIndex].Replace("\r\n", "<br />"));

        var linkTag = new TagBuilder("a");
        linkTag.AddCssClass("fw-light");

        var normalised = _stringFormatter.ToPrintableNormalizedString(referencedWord);
        var vector = _embeddingsModel.CalculateVector(normalised);
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

        linkTag.InnerHtml.AppendHtml(referencedWord.ToHtmlString());

        output.Content.AppendHtml(linkTag);
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
}
