using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;

namespace Shekinah.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var failures = new List<string>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, ct);
            failures.AddRange(result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
        }

        if (failures.Count > 0)
        {
            return Result.Failure<TResponse>(Error.Validation("Validation.Failed", string.Join(" | ", failures)));
        }

        return await next();
    }
}
