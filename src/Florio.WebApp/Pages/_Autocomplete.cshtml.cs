using Florio.Data;
using Florio.VectorEmbeddings.EmbeddingsModel;
using Florio.VectorEmbeddings.Repositories;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Florio.WebApp.Pages;

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
            var vector = _embeddingsModel.CalculateVector(_stringFormatter.ToPrintableNormalizedString(Search));

            Results = await _repository
                .FindByWord(vector, HttpContext.RequestAborted)
                .Select(wd => _stringFormatter.ToPrintableNormalizedString(wd.Word))
                .ToListAsync();
        }
        return Page();
    }
}
