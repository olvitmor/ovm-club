using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace WebApi.Time;

public class UtcDateTimeModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        return TimeModelBinding.Bind<DateTime>(
            bindingContext,
            IsoTimeParser.TryParseUtcDateTime,
            "must be an ISO 8601 date and time, for example 2026-09-22T10:00:00Z");
    }
}

public class DateOnlyModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        return TimeModelBinding.Bind<DateOnly>(
            bindingContext,
            IsoTimeParser.TryParseDate,
            "must be a date without time, for example 2026-09-22");
    }
}

public class TimeModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var type = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;

        if (type == typeof(DateTime))
        {
            return new BinderTypeModelBinder(typeof(UtcDateTimeModelBinder));
        }

        if (type == typeof(DateOnly))
        {
            return new BinderTypeModelBinder(typeof(DateOnlyModelBinder));
        }

        return null;
    }
}

internal static class TimeModelBinding
{
    public delegate bool TryParse<T>(string? value, out T result);

    public static Task Bind<T>(ModelBindingContext bindingContext, TryParse<T> tryParse, string error)
    {
        var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

        if (valueResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);

        if (tryParse(valueResult.FirstValue, out var parsed))
        {
            bindingContext.Result = ModelBindingResult.Success(parsed);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"{bindingContext.ModelName} {error}");
        }

        return Task.CompletedTask;
    }
}
