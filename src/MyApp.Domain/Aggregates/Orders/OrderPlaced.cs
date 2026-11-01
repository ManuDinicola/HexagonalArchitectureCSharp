using System.Runtime.InteropServices;
using MyApp.Domain.Common;

namespace MyApp.Domain.Aggregates.Orders;

[StructLayout(LayoutKind.Auto)]
public readonly record struct OrderPlaced(Guid OrderId, string CustomerId, decimal Total, int LineCount, DateTimeOffset OccurredAt) : IDomainEvent
{
    public const string TypeName = "order.placed.v1";

    public string Type => TypeName;
}
