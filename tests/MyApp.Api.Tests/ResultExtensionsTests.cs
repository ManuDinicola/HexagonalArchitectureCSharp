using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using MyApp.Api.Endpoints;
using MyApp.Domain.Common;

namespace MyApp.Api.Tests;

public sealed class ResultExtensionsTests
{
    private static readonly Error NotFound = Error.NotFound("A.NotFound", "missing");
    private static readonly Error Conflict = Error.Conflict("B.Conflict", "clash");
    private static readonly Error Invalid = Error.Validation("C.Invalid", "bad", "field");

    [Fact]
    public void Mixed_errors_pick_the_status_of_the_first_non_validation_error_but_list_every_error()
    {
        var problem = new Failed([Invalid, NotFound, Conflict]).ToProblem().ShouldBeOfType<ProblemHttpResult>().ProblemDetails;

        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
        problem.Title.ShouldBe("A.NotFound");
        var listed = problem.Extensions["errors"].ShouldBeAssignableTo<System.Collections.IEnumerable>()!.Cast<object>().ToList();
        listed.Count.ShouldBe(3);
    }

    [Fact]
    public void Only_validation_errors_are_a_validation_problem_keyed_by_target()
    {
        var result = new Failed([Invalid, Error.Validation("D.Other", "worse", "field")]).ToProblem();

        var problem = result.ShouldBeOfType<ProblemHttpResult>().ProblemDetails.ShouldBeAssignableTo<HttpValidationProblemDetails>();
        problem.Errors["field"].ShouldBe(["bad", "worse"]);
    }

    [Fact]
    public void A_failure_without_errors_is_a_500()
    {
        var problem = new Failed([]).ToProblem().ShouldBeOfType<ProblemHttpResult>().ProblemDetails;

        problem.Status.ShouldBe(StatusCodes.Status500InternalServerError);
    }
}
