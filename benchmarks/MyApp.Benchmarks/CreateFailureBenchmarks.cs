using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Candidate = MyApp.Domain.Common;
using Keyword = MyApp.Benchmarks.KeywordUnion;

namespace MyApp.Benchmarks;

/// <summary>Producing a failure from existing errors. Allocates the <c>Failed</c> case on both sides.</summary>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class CreateFailureBenchmarks
{
    private IReadOnlyList<Keyword.Error> _keywordErrors = [];
    private IReadOnlyList<Candidate.Error> _candidateErrors = [];

    [GlobalSetup]
    public void Setup()
    {
        _keywordErrors = Errors.Baseline;
        _candidateErrors = Errors.Candidate;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CreateFailure")]
    public Keyword.Result<Guid> Keyword_union() => Keyword.Result.Failure<Guid>(_keywordErrors);

    [Benchmark]
    [BenchmarkCategory("CreateFailure")]
    public Candidate.Result<Guid> Handwritten_union() => Candidate.Result.Failure<Guid>(_candidateErrors);
}
