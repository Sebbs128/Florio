using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Florio.WebApp.Pages;

[ResponseCache(Duration = 30 * 60)]
public class AboutModel : PageModel
{
    public void OnGet()
    {
    }
}
