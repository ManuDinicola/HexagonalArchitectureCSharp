namespace MyApp.Benchmarks;

/// <summary>One non-default sample value per payload type, so every benchmark class builds the same input.</summary>
internal static class Sample<T>
{
    public static T Value { get; } = Create();

    private static T Create()
    {
        object sample = typeof(T) switch
        {
            var t when t == typeof(Guid) => Guid.Parse("0197a1a2-0000-7000-8000-000000000001"),
            var t when t == typeof(int) => 42,
            var t when t == typeof(Point) => new Point(1, 2),
            var t when t == typeof(string) => "order-42",
            var t when t == typeof(Payload) => new Payload("widget", 3),
            var t when t == typeof(BigStruct) => new BigStruct(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16),
            var t when t == typeof(BigDto) => new BigDto(
                Guid.Parse("0197a1a2-0000-7000-8000-000000000001"),
                "customer-1",
                DateTimeOffset.UnixEpoch,
                Enumerable.Range(0, 100).Select(i => new BigDtoLine($"sku-{i}", i + 1, 9.99m)).ToList()),
            _ => throw new NotSupportedException(typeof(T).Name),
        };

        return (T)sample;
    }
}
