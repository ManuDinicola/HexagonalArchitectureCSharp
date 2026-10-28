using MyApp.Application.ReadModels;

namespace MyApp.Application.Ports.Out;

/// <summary>Read side for lists and other shapes that are not an aggregate. Queries only; it never changes state.</summary>
public interface IOrderReadStore
{
    /// <summary>Newest first. <paramref name="skip"/> and <paramref name="take"/> are already validated by the use case.</summary>
    Task<IReadOnlyList<OrderSummary>> ListNewestFirstAsync(int skip, int take, CancellationToken cancellationToken);
}
