using System.Text.Encodings.Web;

using Florio.Data;
using Florio.WebApp.Extensions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Florio.WebApp.TagHelpers;

[HtmlTargetElement("render", TagStructure = TagStructure.NormalOrSelfClosing)]
public class StringWithReferenceLinksTagHelper(IStringFormatter stringFormatter, IUrlHelperFactory urlHelperFactory) : TagHelper
{
    private readonly IStringFormatter _stringFormatter = stringFormatter;
    private readonly IUrlHelperFactory _urlHelperFactory = urlHelperFactory;

    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the <see cref="Rendering.ViewContext"/> for the current request.
    /// </summary>
    [HtmlAttributeNotBound]
    [ViewContext]
    public ViewContext ViewContext { get; set; }

    [HtmlAttributeName("referenced-words")]
    public string[]? ReferencedWords { get; set; }

    private HtmlEncoder HtmlEncoder => HtmlEncoder.Default;
    private UrlEncoder UrlEncoder => UrlEncoder.Default;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var urlHelper = _urlHelperFactory.GetUrlHelper(ViewContext);
        var content = await output.GetChildContentAsync();
        var text = content.GetContent(HtmlEncoder);

        var splitText = text
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select((s, i) => (Text: s, Index: i));

        output.TagName = "span";

        foreach (var item in splitText)
        {
            if (item.Index % 2 == 0)
            {
                output.Content.AppendHtml($"<i>{item.Text.ToHtmlString()}</i>");
                continue;
            }

            if (ReferencedWords is null)
            {
                output.Content.AppendHtml(item.Text.ToHtmlString());
                continue;
            }

            var refWord = ReferencedWords[item.Index / 2];
            var htmlEncodedRefWord = HtmlEncoder.Encode(refWord);
            var replaceFrom = item.Text.IndexOf(htmlEncodedRefWord);
            var link = urlHelper.Page("Italian", new { word = _stringFormatter.ToPrintableNormalizedString(refWord) });
            string tag = $"""<a class="fw-light" href="{link}">{refWord.ToHtmlString()}</a>""";
            output.Content.AppendHtml($"{item.Text[..replaceFrom]}{tag}{item.Text[(replaceFrom + htmlEncodedRefWord.Length)..]}");
        }
    }
}
