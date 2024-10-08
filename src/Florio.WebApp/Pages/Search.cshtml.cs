using Florio.Data;
using Florio.VectorEmbeddings.EmbeddingsModel;
using Florio.VectorEmbeddings.Repositories;
using Florio.WebApp.Binders;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Florio.WebApp.Pages;

[OutputCache(VaryByQueryKeys = [nameof(Term)], Duration = 300)]
[ResponseCache(VaryByQueryKeys = [nameof(Term)], Duration = 30 * 60)]
public class SearchModel(
    IVectorEmbeddingModelFactory embeddingsModelFactory,
    IWordDefinitionRepository repository,
    IStringFormatter stringFormatter) : PageModel
{
    private readonly VectorEmbeddingModel _embeddingsModel = embeddingsModelFactory.GetModel();
    private readonly IWordDefinitionRepository _repository = repository;
    private readonly IStringFormatter _stringFormatter = stringFormatter;

    [BindProperty(BinderType = typeof(UrlEncodedStringBinder), SupportsGet = true)]
    public string? Term { get; set; }

    public IAsyncEnumerable<WordDefinition> Results { get; set; } = AsyncEnumerable.Empty<WordDefinition>();

    public IActionResult OnGet()
    {
        if (string.IsNullOrWhiteSpace(Term))
        {
            return Page();
        }

        var vector = _embeddingsModel.CalculateVector(_stringFormatter.NormalizeForVector(Term));

        Results = _repository.FindMatches(vector, 10, HttpContext.RequestAborted);

        return Page();
    }
}
