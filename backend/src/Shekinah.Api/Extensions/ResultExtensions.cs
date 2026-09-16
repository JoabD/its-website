using Microsoft.AspNetCore.Mvc;
using Shekinah.Domain.Common;

namespace Shekinah.Api.Extensions;

/// <summary>
/// Traduce Result&lt;T&gt; a ProblemDetails RFC 9457 (spec técnico §3.6). Nunca se expone
/// exception.Message al cliente — solo Error.Message, que ya es un mensaje de negocio pensado
/// para el usuario final.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToApiResult<T>(this Result<T> result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return successStatusCode == StatusCodes.Status201Created
                ? Results.Created(string.Empty, result.Value)
                : Results.Json(result.Value, statusCode: successStatusCode);
        }

        return ToProblem(result.Error);
    }

    public static IResult ToApiResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return ToProblem(result.Error);
    }

    private static IResult ToProblem(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["traceId"] = System.Diagnostics.Activity.Current?.Id });
    }
}
