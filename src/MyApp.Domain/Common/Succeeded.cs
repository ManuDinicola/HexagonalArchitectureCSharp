namespace MyApp.Domain.Common;

/// <summary>The success case of a value-less <see cref="Result"/>. It carries no data, so use the shared <see cref="Instance"/>.</summary>
public sealed record Succeeded
{
    public static Succeeded Instance { get; } = new();
}
