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
            _ => throw new NotSupportedException(typeof(T).Name),
        };

        return (T)sample;
    }
}
