using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
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

    private static readonly Uri OrdersUri = new("/v1/orders", UriKind.Relative);

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
        body.RootElement.GetProperty("status").GetString().ShouldBe("Placed");
        body.RootElement.GetProperty("lines").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Listing_returns_the_newest_orders_first_and_pages()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync(OrdersUri, ValidOrder);
        var second = await (await client.PostAsJsonAsync(OrdersUri, ValidOrder)).Content.ReadAsStringAsync();
        using var secondId = JsonDocument.Parse(second);

        var response = await client.GetAsync(new Uri("/v1/orders?page=1&pageSize=1", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetArrayLength().ShouldBe(1);
        body.RootElement[0].GetProperty("id").GetGuid().ShouldBe(secondId.RootElement.GetProperty("orderId").GetGuid());
        body.RootElement[0].GetProperty("total").GetDecimal().ShouldBe(9m);
        body.RootElement[0].GetProperty("lineCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Listing_with_a_bad_page_is_a_400_keyed_by_the_field()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/v1/orders?page=0", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").TryGetProperty("page", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Unknown_order_is_a_404_problem()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri($"/v1/orders/{Guid.NewGuid()}", UriKind.Relative));

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
    public async Task Cancelling_an_order_is_a_204_and_the_order_then_reads_as_cancelled()
    {
        using var client = _factory.CreateClient();
        var location = (await client.PostAsJsonAsync(OrdersUri, ValidOrder)).Headers.Location.ShouldNotBeNull();

        var cancelled = await client.PostAsync(new Uri($"{location}/cancel", UriKind.Relative), content: null);

        cancelled.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var body = JsonDocument.Parse(await client.GetStringAsync(location));
        body.RootElement.GetProperty("status").GetString().ShouldBe("Cancelled");
    }

    [Fact]
    public async Task A_cancelled_order_shows_as_cancelled_in_the_list_with_its_total_unchanged()
    {
        using var client = _factory.CreateClient();
        var location = (await client.PostAsJsonAsync(OrdersUri, ValidOrder)).Headers.Location.ShouldNotBeNull();
        await client.PostAsync(new Uri($"{location}/cancel", UriKind.Relative), content: null);

        using var body = JsonDocument.Parse(await client.GetStringAsync(OrdersUri));

        body.RootElement[0].GetProperty("status").GetString().ShouldBe("Cancelled");
        body.RootElement[0].GetProperty("total").GetDecimal().ShouldBe(9m);
    }

    [Fact]
    public async Task Cancelling_twice_is_a_409_problem()
    {
        using var client = _factory.CreateClient();
        var location = (await client.PostAsJsonAsync(OrdersUri, ValidOrder)).Headers.Location.ShouldNotBeNull();
        var cancelUri = new Uri($"{location}/cancel", UriKind.Relative);
        await client.PostAsync(cancelUri, content: null);

        var again = await client.PostAsync(cancelUri, content: null);

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var problem = JsonDocument.Parse(await again.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("title").GetString().ShouldBe("Order.AlreadyCancelled");
    }

    [Fact]
    public async Task Cancelling_an_unknown_order_is_a_404_problem()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync(new Uri($"/v1/orders/{Guid.NewGuid()}/cancel", UriKind.Relative), content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<HttpResponseMessage> PostWithKeyAsync(HttpClient client, string key, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, OrdersUri) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Retrying_a_post_with_the_same_idempotency_key_returns_the_same_order_and_creates_no_second_one()
    {
        using var client = _factory.CreateClient();

        var first = await PostWithKeyAsync(client, "retry-1", ValidOrder);
        var retry = await PostWithKeyAsync(client, "retry-1", ValidOrder);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        retry.StatusCode.ShouldBe(HttpStatusCode.Created);
        retry.Headers.Location.ShouldBe(first.Headers.Location);
        using var list = JsonDocument.Parse(await client.GetStringAsync(OrdersUri));
        list.RootElement.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task The_same_idempotency_key_with_another_body_is_a_409_problem()
    {
        using var client = _factory.CreateClient();
        await PostWithKeyAsync(client, "retry-2", ValidOrder);

        var other = await PostWithKeyAsync(client, "retry-2", new { customerId = "someone-else", lines = new[] { new { sku = "A", quantity = 1, unitPrice = 1m } } });

        other.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var problem = JsonDocument.Parse(await other.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("title").GetString().ShouldBe("Idempotency.KeyReused");
    }

    [Fact]
    public async Task Without_an_idempotency_key_every_post_creates_an_order()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync(OrdersUri, ValidOrder);
        await client.PostAsJsonAsync(OrdersUri, ValidOrder);

        using var list = JsonDocument.Parse(await client.GetStringAsync(OrdersUri));

        list.RootElement.GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task A_blank_idempotency_key_is_a_400_keyed_by_the_header()
    {
        using var client = _factory.CreateClient();

        var response = await PostWithKeyAsync(client, " ", ValidOrder);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").TryGetProperty("Idempotency-Key", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Placing_an_order_publishes_OrderPlaced_through_the_outbox()
    {
        var sent = new System.Collections.Concurrent.ConcurrentQueue<MyApp.Application.Outbox.OutgoingMessage>();
        var publisher = A.Fake<IMessagePublisher>();
        A.CallTo(() => publisher.PublishAsync(A<MyApp.Application.Outbox.OutgoingMessage>._, A<CancellationToken>._))
            .Invokes((MyApp.Application.Outbox.OutgoingMessage m, CancellationToken _) => sent.Enqueue(m))
            .Returns(Task.CompletedTask);
        using var factory = new ApiFactory
        {
            Settings = { ["Outbox:PollIntervalSeconds"] = "0.05" },
            ConfigureTestServices = services => services.AddSingleton(publisher),
        };
        using var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync(OrdersUri, ValidOrder);
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());

        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (sent.IsEmpty && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        var message = sent.ShouldHaveSingleItem();
        message.Type.ShouldBe("order.placed.v1");
        using var payload = JsonDocument.Parse(message.Payload);
        payload.RootElement.GetProperty("OrderId").GetGuid().ShouldBe(createdBody.RootElement.GetProperty("orderId").GetGuid());
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
