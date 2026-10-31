using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Candidate = MyApp.Benchmarks.HandwrittenUnion;
using Keyword = MyApp.Benchmarks.KeywordUnion;

namespace MyApp.Benchmarks;

/// <summary>Consuming an existing <c>Result&lt;Guid&gt;</c> (what PlaceOrder returns): the handler, behavior and endpoint path.</summary>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ReadValueTypeBenchmarks
{
    private Keyword.Result<Guid> _keywordOk;
    private Keyword.Result<Guid> _keywordBad;
    private Candidate.Result<Guid> _candidateOk;
    private Candidate.Result<Guid> _candidateBad;

    [GlobalSetup]
    public void Setup()
    {
        _keywordOk = Keyword.Result.Success(Sample<Guid>.Value);
        _keywordBad = Keyword.Result.Failure<Guid>(Errors.Baseline);
        _candidateOk = Candidate.Result.Success(Sample<Guid>.Value);
        _candidateBad = Candidate.Result.Failure<Guid>(Errors.Candidate);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SwitchSuccess")]
    public int Keyword_switch_success() => Consume(_keywordOk);

    [Benchmark]
    [BenchmarkCategory("SwitchSuccess")]
    public int Handwritten_switch_success() => Consume(_candidateOk);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SwitchFailure")]
    public int Keyword_switch_failure() => Consume(_keywordBad);

    [Benchmark]
    [BenchmarkCategory("SwitchFailure")]
    public int Handwritten_switch_failure() => Consume(_candidateBad);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("IsSuccessErrors")]
    public int Keyword_is_success_and_errors() => (_keywordOk.IsSuccess ? 1 : 0) + _keywordBad.Errors.Count;

    [Benchmark]
    [BenchmarkCategory("IsSuccessErrors")]
    public int Handwritten_is_success_and_errors() => (_candidateOk.IsSuccess ? 1 : 0) + _candidateBad.Errors.Count;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Consume(Keyword.Result<Guid> result) => result switch
    {
        Keyword.Failed failed => failed.Errors.Count,
        Guid id => id.GetHashCode(),
    };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Consume(Candidate.Result<Guid> result) => result switch
    {
        Candidate.Failed failed => failed.Errors.Count,
        Guid id => id.GetHashCode(),
    };
}
