using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FakeItEasy;
using MyApp.Application.Ports.Out;
using MyApp.Domain.Common;

namespace MyApp.Api.Tests;

public sealed class OrderEndpointsTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    private static readonly object ValidOrder = new
    {
        customerId = "cust-1",
        lines = new[] { new { sku = "SKU-1", quantity = 2, unitPrice = 4.5m } },
    };

    private static readonly Uri OrdersUri = new("/orders", UriKind.Relative);

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Placing_an_order_returns_201_with_a_location_and_the_order_can_be_read_back()
    {
        using var client = _factory.CreateClient();

        var created = await client.PostAsJsonAsync(OrdersUri, ValidOrder);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var location = created.Headers.Location.ShouldNotBeNull();

        var fetched = await client.GetAsync(location);
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("customerId").GetString().ShouldBe("cust-1");
        body.RootElement.GetProperty("total").GetDecimal().ShouldBe(9m);
        body.RootElement.GetProperty("lines").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Unknown_order_is_a_404_problem()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri($"/orders/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("title").GetString().ShouldBe("Order.NotFound");
    }

    [Fact]
    public async Task Missing_fields_are_a_400_whose_error_keys_are_the_field_paths()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(OrdersUri, new { customerId = "", lines = new[] { new { sku = "", quantity = 1, unitPrice = 1m } } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var keys = problem.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToList();
        keys.ShouldBe(["customerId", "lines[0].sku"], ignoreOrder: true);
    }

    [Fact]
    public async Task Broken_business_rules_are_a_400_with_every_error_at_once()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(OrdersUri, new { customerId = "cust-1", lines = new[] { new { sku = "A", quantity = 0, unitPrice = -1m } } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var keys = problem.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToList();
        keys.ShouldBe(["lines[0].quantity", "lines[0].unitPrice"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_missing_body_field_does_not_crash()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(OrdersUri, new { customerId = "cust-1" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_storage_conflict_is_a_409_problem()
    {
        var unitOfWork = A.Fake<IUnitOfWork>();
        A.CallTo(() => unitOfWork.SaveChangesAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<Result>(Error.Conflict("Persistence.DuplicateKey", "duplicate")));
        using var factory = new ApiFactory { UnitOfWorkOverride = unitOfWork };
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(OrdersUri, ValidOrder);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
