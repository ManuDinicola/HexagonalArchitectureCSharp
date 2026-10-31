using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Candidate = MyApp.Benchmarks.HandwrittenUnion;
using Keyword = MyApp.Benchmarks.KeywordUnion;

namespace MyApp.Benchmarks;

/// <summary>Consuming an existing <c>Result&lt;Guid&gt;</c> (what PlaceOrder returns): the handler, behavior and endpoint path.</summary>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ReadBigStructBenchmarks
{
    private Keyword.Result<BigStruct> _keywordOk;
    private Keyword.Result<BigStruct> _keywordBad;
    private Candidate.Result<BigStruct> _candidateOk;
    private Candidate.Result<BigStruct> _candidateBad;

    [GlobalSetup]
    public void Setup()
    {
        _keywordOk = Keyword.Result.Success(Sample<BigStruct>.Value);
        _keywordBad = Keyword.Result.Failure<BigStruct>(Errors.Baseline);
        _candidateOk = Candidate.Result.Success(Sample<BigStruct>.Value);
        _candidateBad = Candidate.Result.Failure<BigStruct>(Errors.Candidate);
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
    private static int Consume(Keyword.Result<BigStruct> result) => result switch
    {
        Keyword.Failed failed => failed.Errors.Count,
        BigStruct big => (int)big.A,
    };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Consume(Candidate.Result<BigStruct> result) => result switch
    {
        Candidate.Failed failed => failed.Errors.Count,
        BigStruct big => (int)big.A,
    };
}
