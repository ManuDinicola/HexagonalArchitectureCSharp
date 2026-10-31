using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Candidate = MyApp.Benchmarks.HandwrittenUnion;
using Keyword = MyApp.Benchmarks.KeywordUnion;

namespace MyApp.Benchmarks;

/// <summary>The value-less <c>Result</c>: creating a success and a failure, then consuming them with a switch.</summary>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class VoidResultBenchmarks
{
    private IReadOnlyList<Keyword.Error> _keywordErrors = [];
    private IReadOnlyList<Candidate.Error> _candidateErrors = [];
    private Keyword.Result _keywordOk;
    private Keyword.Result _keywordBad;
    private Candidate.Result _candidateOk;
    private Candidate.Result _candidateBad;

    [GlobalSetup]
    public void Setup()
    {
        _keywordErrors = Errors.Baseline;
        _candidateErrors = Errors.Candidate;
        _keywordOk = Keyword.Result.Success();
        _keywordBad = Keyword.Result.Failure(_keywordErrors);
        _candidateOk = Candidate.Result.Success();
        _candidateBad = Candidate.Result.Failure(_candidateErrors);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("VoidCreateSuccess")]
    public Keyword.Result Keyword_create_success() => Keyword.Result.Success();

    [Benchmark]
    [BenchmarkCategory("VoidCreateSuccess")]
    public Candidate.Result Handwritten_create_success() => Candidate.Result.Success();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("VoidCreateFailure")]
    public Keyword.Result Keyword_create_failure() => Keyword.Result.Failure(_keywordErrors);

    [Benchmark]
    [BenchmarkCategory("VoidCreateFailure")]
    public Candidate.Result Handwritten_create_failure() => Candidate.Result.Failure(_candidateErrors);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("VoidSwitch")]
    public int Keyword_switch() => Consume(_keywordOk) + Consume(_keywordBad);

    [Benchmark]
    [BenchmarkCategory("VoidSwitch")]
    public int Handwritten_switch() => Consume(_candidateOk) + Consume(_candidateBad);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Consume(Keyword.Result result) => result switch
    {
        Keyword.Failed failed => failed.Errors.Count,
        Keyword.Succeeded => 1,
    };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Consume(Candidate.Result result) => result switch
    {
        Candidate.Failed failed => failed.Errors.Count,
        Candidate.Succeeded => 1,
    };
}
