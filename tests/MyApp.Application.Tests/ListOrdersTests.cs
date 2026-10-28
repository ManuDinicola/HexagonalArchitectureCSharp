using FakeItEasy;
using MyApp.Application.ReadModels;
using MyApp.Application.UseCases.ListOrders;
using MyApp.Domain.Common;

namespace MyApp.Application.Tests;

public class ListOrdersTests : IAsyncLifetime
{
    private readonly TestApp _app = new();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Second_page_asks_the_read_store_to_skip_the_first_page()
    {
        var row = new OrderSummary(Guid.CreateVersion7(TestApp.Now), "cust-1", TestApp.Now, 9m, 1);
        A.CallTo(() => _app.ReadStore.ListNewestFirstAsync(10, 10, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<OrderSummary>>([row]));

        var result = await _app.SendAsync(new ListOrdersQuery(Page: 2, PageSize: 10));

        result.Unwrap().ShouldBe([row]);
    }

    [Theory]
    [InlineData(0, 10, "page")]
    [InlineData(1, 0, "pageSize")]
    [InlineData(1, ListOrdersQuery.MaxPageSize + 1, "pageSize")]
    public async Task Bad_paging_is_a_validation_error_pointing_at_the_field(int page, int pageSize, string target)
    {
        var result = await _app.SendAsync(new ListOrdersQuery(page, pageSize));

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldAllBe(e => e.Type == ErrorType.Validation);
        result.Errors.ShouldContain(e => e.Target == target);
        A.CallTo(() => _app.ReadStore.ListNewestFirstAsync(A<int>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task A_query_never_saves()
    {
        await _app.SendAsync(new ListOrdersQuery());

        A.CallTo(() => _app.UnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }
}
