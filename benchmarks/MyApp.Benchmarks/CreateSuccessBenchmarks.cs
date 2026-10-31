using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Candidate = MyApp.Benchmarks.HandwrittenUnion;
using Keyword = MyApp.Benchmarks.KeywordUnion;

namespace MyApp.Benchmarks;

/// <summary>Producing a success <c>Result&lt;T&gt;</c> only (nothing reads it), for a value type, a struct, a string and a record class.</summary>
[GenericTypeArguments(typeof(Guid))]
[GenericTypeArguments(typeof(int))]
[GenericTypeArguments(typeof(Point))]
[GenericTypeArguments(typeof(string))]
[GenericTypeArguments(typeof(Payload))]
[GenericTypeArguments(typeof(BigStruct))]
[GenericTypeArguments(typeof(BigDto))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory, BenchmarkLogicalGroupRule.ByParams)]
[CategoriesColumn]
public class CreateSuccessBenchmarks<T>
{
    private T _value = default!;

    [GlobalSetup]
    public void Setup() => _value = Sample<T>.Value;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CreateSuccess")]
    public Keyword.Result<T> Keyword_union() => Keyword.Result.Success(_value);

    [Benchmark]
    [BenchmarkCategory("CreateSuccess")]
    public Candidate.Result<T> Handwritten_union() => Candidate.Result.Success(_value);
}
