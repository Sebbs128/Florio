using Microsoft.AspNetCore.Html;

namespace Florio.WebApp.Extensions;

public static class StringExtensions
{
    public static HtmlString ToHtmlString(this string input) =>
        new(input
            .Replace("[", "<i>")
            .Replace("]", "</i>"));
}
