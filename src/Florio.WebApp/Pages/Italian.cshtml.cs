using Florio.Data;
using Florio.VectorEmbeddings.EmbeddingsModel;
using Florio.VectorEmbeddings.Repositories;
using Florio.WebApp.Binders;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Florio.WebApp.Pages;

[OutputCache(VaryByRouteValueNames = [nameof(Word)], Duration = 300)]
public class ItalianModel(
    IVectorEmbeddingModelFactory embeddingsModelFactory,
    IWordDefinitionRepository repository,
    IStringFormatter stringFormatter) : PageModel
{
    private readonly VectorEmbeddingModel _embeddingsModel = embeddingsModelFactory.GetModel();
    private readonly IWordDefinitionRepository _repository = repository;
    private readonly IStringFormatter _stringFormatter = stringFormatter;

    [BindProperty(BinderType = typeof(UrlEncodedStringBinder), SupportsGet = true)]
    public string? Word { get; set; }

    public IAsyncEnumerable<WordDefinition> WordDefinition { get; set; } = AsyncEnumerable.Empty<WordDefinition>();

    public async Task<IActionResult> OnGet()
    {
        if (string.IsNullOrWhiteSpace(Word))
        {
            return RedirectToPage("Search");
        }

        var vector = _embeddingsModel.CalculateVector(_stringFormatter.NormalizeForVector(Word));

        WordDefinition = _repository.FindClosestMatch(vector, HttpContext.RequestAborted);

        if (!await WordDefinition.AnyAsync())
        {
            return RedirectToPage("Search", new { Term = Word });
        }

        return Page();
    }
}
