namespace A2S.Application.Common;

/// <summary>
/// Static abstract factory for creating typed failure results without reflection.
/// Implemented by commands used with AuthorizedWorkoutBehavior.
/// </summary>
public interface IFailureFactory<TResponse> where TResponse : Result
{
    static abstract TResponse CreateFailure(string error, ErrorCode code);
}
