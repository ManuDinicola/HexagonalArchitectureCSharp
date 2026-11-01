using FakeItEasy;
using MyApp.Application.Idempotency;
using MyApp.Application.UseCases.PlaceOrder;
using MyApp.Domain.Aggregates.Orders;
using MyApp.Domain.Common;

namespace MyApp.Application.Tests;

public sealed class IdempotencyTests : IAsyncLifetime
{
    private readonly TestApp _app = new();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _app.DisposeAsync();

    private static PlaceOrderCommand Command(string? key, int quantity = 2) =>
        new("cust-1", [new PlaceOrderLine("SKU-1", quantity, 10m)], key);

    [Fact]
    public async Task A_retry_with_the_same_key_and_body_answers_with_the_first_result_and_places_nothing_new()
    {
        var first = await _app.SendAsync(Command("key-1"));
        var retry = await _app.SendAsync(Command("key-1"));

        retry.Unwrap().ShouldBe(first.Unwrap());
        _app.Saved.ShouldHaveSingleItem();
        _app.Idempotency.Saved.ShouldHaveSingleItem().Key.ShouldBe("key-1");
        A.CallTo(() => _app.Orders.Add(A<Order>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task The_same_key_with_another_body_is_a_conflict_and_places_nothing()
    {
        await _app.SendAsync(Command("key-1"));

        var other = await _app.SendAsync(Command("key-1", quantity: 3));

        other.Errors.ShouldHaveSingleItem().ShouldBe(IdempotencyErrors.KeyReused);
        _app.Saved.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Without_a_key_every_call_places_an_order_and_nothing_is_recorded()
    {
        var first = await _app.SendAsync(Command(null));
        var second = await _app.SendAsync(Command(null));

        second.Unwrap().ShouldNotBe(first.Unwrap());
        _app.Saved.Count.ShouldBe(2);
        _app.Idempotency.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_command_is_not_recorded_so_the_key_can_be_used_again_with_a_corrected_body()
    {
        var failed = await _app.SendAsync(Command("key-1", quantity: 0));
        failed.IsFailure.ShouldBeTrue();
        _app.Idempotency.Saved.ShouldBeEmpty();

        var corrected = await _app.SendAsync(Command("key-1"));

        corrected.IsSuccess.ShouldBeTrue();
        _app.Idempotency.Saved.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task The_same_key_is_independent_per_operation_type()
    {
        await _app.SendAsync(Command("key-1"));

        _app.Idempotency.Saved.ShouldHaveSingleItem().Scope.ShouldBe(nameof(PlaceOrderCommand));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_key_is_a_validation_error_on_the_header(string key)
    {
        var result = await _app.SendAsync(Command(key));

        result.Errors.ShouldContain(e => e.Target == IdempotencyKeys.HeaderName && e.Type == ErrorType.Validation);
        _app.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_key_that_is_too_long_is_a_validation_error_on_the_header()
    {
        var result = await _app.SendAsync(Command(new string('k', IdempotencyKeys.MaxLength + 1)));

        result.Errors.ShouldContain(e => e.Target == IdempotencyKeys.HeaderName);
    }
}
