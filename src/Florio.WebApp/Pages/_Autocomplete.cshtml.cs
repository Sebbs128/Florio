using Florio.Data;
using Florio.VectorEmbeddings.EmbeddingsModel;
using Florio.VectorEmbeddings.Repositories;

using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Florio.WebApp.Pages;

[EnableCors]
[OutputCache(VaryByQueryKeys = [nameof(Search)], Duration = 300)]
[ResponseCache(VaryByQueryKeys = [nameof(Search)], Duration = 30 * 60)]
public class AutocompleteModel(
    IVectorEmbeddingModelFactory embeddingsModel,
    IWordDefinitionRepository repository,
    IStringFormatter stringFormatter) : PageModel
{
    private readonly VectorEmbeddingModel _embeddingsModel = embeddingsModel.GetModel();
    private readonly IWordDefinitionRepository _repository = repository;
    private readonly IStringFormatter _stringFormatter = stringFormatter;

    [BindProperty(SupportsGet = true)]
    public string Search { get; set; } = default!;

    public IEnumerable<string> Results { get; set; } = [];

    public async Task<IActionResult> OnGet()
    {
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var vector = _embeddingsModel.CalculateVector(_stringFormatter.NormalizeForVector(Search));

            Results = await _repository
                .FindByWord(vector, HttpContext.RequestAborted)
                .Select(wd => _stringFormatter.ToPrintableNormalizedString(wd.Word))
                .ToListAsync();
        }
        return Page();
    }
}
