using System.Runtime.InteropServices;
using MyApp.Domain.Common;

namespace MyApp.Domain.Aggregates.Orders;

[StructLayout(LayoutKind.Auto)]
public readonly record struct OrderCancelled(Guid OrderId, DateTimeOffset OccurredAt) : IDomainEvent
{
    public const string TypeName = "order.cancelled.v1";

    public string Type => TypeName;
}
