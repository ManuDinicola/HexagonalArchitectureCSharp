using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Candidate = MyApp.Domain.Common;
using Keyword = MyApp.Benchmarks.KeywordUnion;

namespace MyApp.Benchmarks;

/// <summary>
/// A mini validation behavior, generic over the response like the real pipeline: it either short-circuits through
/// <c>TResponse.FromErrors</c> or calls the next step.
/// </summary>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class PipelineBenchmarks
{
    private static readonly Func<Keyword.Result<Guid>> KeywordNext = static () => Keyword.Result.Success(Sample<Guid>.Value);
    private static readonly Func<Candidate.Result<Guid>> CandidateNext = static () => Candidate.Result.Success(Sample<Guid>.Value);

    private IReadOnlyList<Keyword.Error> _keywordErrors = [];
    private IReadOnlyList<Candidate.Error> _candidateErrors = [];

    [GlobalSetup]
    public void Setup()
    {
        _keywordErrors = Errors.Baseline;
        _candidateErrors = Errors.Candidate;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PipelineInvalid")]
    public Keyword.Result<Guid> Keyword_invalid() => Behave(valid: false, _keywordErrors, KeywordNext);

    [Benchmark]
    [BenchmarkCategory("PipelineInvalid")]
    public Candidate.Result<Guid> Handwritten_invalid() => Behave(valid: false, _candidateErrors, CandidateNext);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PipelineValid")]
    public Keyword.Result<Guid> Keyword_valid() => Behave(valid: true, _keywordErrors, KeywordNext);

    [Benchmark]
    [BenchmarkCategory("PipelineValid")]
    public Candidate.Result<Guid> Handwritten_valid() => Behave(valid: true, _candidateErrors, CandidateNext);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static TResponse Behave<TResponse>(bool valid, IReadOnlyList<Keyword.Error> errors, Func<TResponse> next)
        where TResponse : Keyword.IResult<TResponse>
        => valid ? next() : TResponse.FromErrors(errors);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static TResponse Behave<TResponse>(bool valid, IReadOnlyList<Candidate.Error> errors, Func<TResponse> next)
        where TResponse : Candidate.IResult<TResponse>
        => valid ? next() : TResponse.FromErrors(errors);
}
