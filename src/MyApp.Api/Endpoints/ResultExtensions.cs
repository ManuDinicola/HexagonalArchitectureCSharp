using MyApp.Domain.Common;

namespace MyApp.Api.Endpoints;

internal static class ResultExtensions
{
    /// <summary>The single place where expected failures become HTTP ProblemDetails.</summary>
    public static IResult ToProblem(this Failed result)
    {
        var errors = result.Errors;

        if (errors.Count == 0)
        {
            // A failed result without errors (e.g. default(Result)) is a bug, not a client error.
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Result.NoErrors");
        }

        if (errors.All(e => e.Type == ErrorType.Validation))
        {
            var byField = errors
                .GroupBy(e => e.Target ?? e.Code, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray(), StringComparer.Ordinal);
            return Results.ValidationProblem(byField);
        }

        // Mixed errors: a non-validation error is the more important one and picks the status and title,
        // but nothing is dropped: every error is listed under "errors".
        var first = errors.First(e => e.Type != ErrorType.Validation);
        var status = first.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Failure or _ => StatusCodes.Status500InternalServerError,
        };

        var all = errors.Select(e => new { e.Code, e.Message, e.Target }).ToArray();
        return Results.Problem(
            statusCode: status,
            title: first.Code,
            detail: first.Message,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["errors"] = all });
    }
}
