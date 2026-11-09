using A2S.Application.Common;

namespace A2S.Application.Interfaces.Hevy;

/// <summary>Turns a <see cref="HevyError"/> into the <see cref="Result"/> a handler returns.</summary>
public static class HevyErrors
{
    public const string KeyRequired = "Hevy API key is required. Add it under Settings.";

    public static ErrorCode ToErrorCode(this HevyError error) => error.Kind switch
    {
        HevyErrorKind.InvalidKey => ErrorCode.ValidationFailed,
        HevyErrorKind.ProNeeded => ErrorCode.ValidationFailed,
        HevyErrorKind.BadRequest => ErrorCode.ValidationFailed,
        HevyErrorKind.NotFound => ErrorCode.NotFound,
        HevyErrorKind.Conflict => ErrorCode.Conflict,
        _ => ErrorCode.ExternalServiceError
    };

    public static Result<T> ToResult<T>(this HevyError error) => Result.Failure<T>(error.Message, error.ToErrorCode());

    /// <summary>The failure of one Hevy call as a handler result of another type.</summary>
    public static Result<T> FailureOf<T, TOther>(HevyResult<TOther> failed)
    {
        if (failed.Ok)
        {
            throw new InvalidOperationException("FailureOf is for failed Hevy results");
        }

        return failed.Error!.ToResult<T>();
    }
}
