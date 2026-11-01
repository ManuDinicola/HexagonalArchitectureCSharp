namespace MyApp.Application.UseCases.GetOrder;

/// <summary>What leaves the Application layer. Entities never do.</summary>
public sealed record OrderDto(Guid Id, string CustomerId, DateTimeOffset PlacedAt, decimal Total, string Status, IReadOnlyList<OrderLineDto> Lines);
