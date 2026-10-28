namespace MyApp.Domain.Common;

/// <summary>Lets the pipeline build a failed result of any response type without reflection.</summary>
public interface IResult<TSelf> where TSelf : IResult<TSelf>
{
    bool IsSuccess { get; }
    IReadOnlyList<Error> Errors { get; }
    static abstract TSelf FromErrors(IReadOnlyList<Error> errors);
}
