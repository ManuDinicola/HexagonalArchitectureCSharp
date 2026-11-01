namespace MyApp.Domain.Common;

/// <summary>An aggregate root that raises events. The persistence layer collects them when it saves and clears them afterwards.</summary>
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
