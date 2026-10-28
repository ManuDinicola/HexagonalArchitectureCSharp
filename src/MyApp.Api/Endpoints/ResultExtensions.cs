using MyApp.Domain.Common;

namespace MyApp.Api.Endpoints;

internal static class ResultExtensions
{
    /// <summary>The single place where expected failures become HTTP ProblemDetails.</summary>
    public static IResult ToProblem(this Result result)
    {
        var errors = result.Errors;

        if (errors.All(e => e.Type == ErrorType.Validation))
        {
            var byField = errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());
            return Results.ValidationProblem(byField);
        }

        var first = errors[0];
        var status = first.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(statusCode: status, title: first.Code, detail: first.Message);
    }
}
