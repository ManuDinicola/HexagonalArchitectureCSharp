using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyApp.Application.Ports.Out;

namespace MyApp.Api.Tests;

/// <summary>Versioning, health, API docs and correlation: the things around the orders, not the orders themselves.</summary>
public sealed class PlatformEndpointsTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static Uri Relative(string path) => new(path, UriKind.Relative);

    [Fact]
    public async Task Endpoints_live_under_the_version_prefix_and_the_unversioned_path_is_gone()
    {
        using var client = _factory.CreateClient();

        (await client.GetAsync(Relative("/v1/orders"))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync(Relative("/orders"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_probes_answer_200_when_the_app_and_its_database_are_up(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(Relative(path));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [Fact]
    public async Task A_failing_readiness_check_is_a_503_while_liveness_stays_200()
    {
        using var factory = new ApiFactory
        {
            ConfigureTestServices = services => services.AddHealthChecks()
                .AddCheck("broken-dependency", () => HealthCheckResult.Unhealthy("down"), tags: ["ready"]),
        };
        using var client = factory.CreateClient();

        (await client.GetAsync(Relative("/health/ready"))).StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await client.GetAsync(Relative("/health/live"))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_openapi_document_describes_every_versioned_endpoint()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(Relative("/openapi/v1.json"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        paths.ShouldBe(["/v1/orders", "/v1/orders/{id}", "/v1/orders/{id}/cancel"], ignoreOrder: true);
    }

    [Fact]
    public async Task The_idempotency_key_is_documented_as_a_header_of_placing_an_order()
    {
        using var client = _factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync(Relative("/openapi/v1.json")));

        var post = document.RootElement.GetProperty("paths").GetProperty("/v1/orders").GetProperty("post");
        post.GetProperty("parameters").EnumerateArray()
            .Any(p => string.Equals(p.GetProperty("name").GetString(), "Idempotency-Key", StringComparison.Ordinal)
                      && string.Equals(p.GetProperty("in").GetString(), "header", StringComparison.Ordinal))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task The_scalar_reference_is_served()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(Relative("/scalar/v1"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }

    [Fact]
    public async Task An_error_response_carries_the_trace_id_so_support_can_find_the_request()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(Relative($"/v1/orders/{Guid.NewGuid()}"));

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task A_bug_is_a_500_problem_and_not_a_stack_trace()
    {
        var unitOfWork = A.Fake<IUnitOfWork>();
        A.CallTo(() => unitOfWork.SaveChangesAsync(A<CancellationToken>._)).Throws<InvalidOperationException>();
        using var factory = new ApiFactory { UnitOfWorkOverride = unitOfWork };
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            Relative("/v1/orders"),
            new { customerId = "c", lines = new[] { new { sku = "A", quantity = 1, unitPrice = 1m } } });

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("InvalidOperationException");
    }
}
