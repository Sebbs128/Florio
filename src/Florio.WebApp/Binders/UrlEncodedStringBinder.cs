using System.Web;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Florio.WebApp.Binders;

public class UrlEncodedStringBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext is null)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);
        }

        var modelName = bindingContext.ModelName;

        var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);

        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(modelName, valueProviderResult);

        var value = valueProviderResult.FirstValue;

        if (string.IsNullOrWhiteSpace(value))
        {
            return Task.CompletedTask;
        }

        var text = HttpUtility.UrlDecode(value);
        bindingContext.Result = ModelBindingResult.Success(text);
        return Task.CompletedTask;
    }
}
