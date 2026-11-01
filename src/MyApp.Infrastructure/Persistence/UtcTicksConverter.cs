using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MyApp.Infrastructure.Persistence;

/// <summary>
/// Stores a <see cref="DateTimeOffset"/> as UTC ticks (an integer). SQLite cannot compare or order its default text form on the server,
/// and retention needs "older than". Use it on every timestamp a query filters or sorts on.
/// </summary>
internal sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
    value => value.UtcTicks,
    ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
