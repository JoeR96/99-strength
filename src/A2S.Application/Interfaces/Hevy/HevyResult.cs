namespace A2S.Application.Interfaces.Hevy;

/// <summary>What went wrong talking to Hevy, in terms the rest of the app can act on.</summary>
public enum HevyErrorKind
{
    /// <summary>401: the stored key is wrong or revoked.</summary>
    InvalidKey,

    /// <summary>403: the account has no API access (Hevy Pro is required).</summary>
    ProNeeded,

    /// <summary>429: back off for <see cref="HevyError.RetryAfter"/>.</summary>
    RateLimited,

    /// <summary>5xx, timeout, network failure or an open circuit.</summary>
    Unavailable,

    /// <summary>400: Hevy rejected the payload.</summary>
    BadRequest,

    /// <summary>404: the routine, template or measurement is gone on the Hevy side.</summary>
    NotFound,

    /// <summary>409: the resource already exists (body measurement for that date).</summary>
    Conflict
}

/// <summary>A failed Hevy call with a user-facing message. Never carries the key or the request body.</summary>
public sealed record HevyError(HevyErrorKind Kind, string Message, TimeSpan? RetryAfter = null)
{
    public static HevyError InvalidKey() => new(HevyErrorKind.InvalidKey, "Hevy says the key is invalid");

    public static HevyError ProNeeded() => new(HevyErrorKind.ProNeeded, "Hevy Pro is required for the API");

    public static HevyError RateLimited(TimeSpan? retryAfter)
    {
        var seconds = (int)Math.Ceiling((retryAfter ?? TimeSpan.FromSeconds(30)).TotalSeconds);
        return new HevyError(HevyErrorKind.RateLimited, $"Hevy is rate limiting, try again in {seconds} s", retryAfter);
    }

    public static HevyError Unavailable() =>
        new(HevyErrorKind.Unavailable, "Hevy is unavailable, your change is saved locally");

    public static HevyError BadRequest(string? detail) =>
        new(HevyErrorKind.BadRequest, string.IsNullOrWhiteSpace(detail) ? "Hevy rejected the request" : $"Hevy rejected the request: {detail.Trim()}");

    public static HevyError NotFound() => new(HevyErrorKind.NotFound, "Hevy no longer has that item");

    public static HevyError Conflict() => new(HevyErrorKind.Conflict, "Hevy already has that item");
}

/// <summary>Outcome of one Hevy call: a value or a <see cref="HevyError"/>.</summary>
public sealed record HevyResult<T>(bool Ok, T? Value, HevyError? Error)
{
    public static HevyResult<T> Success(T value) => new(true, value, null);

    public static HevyResult<T> Failure(HevyError error) => new(false, default, error);

    /// <summary>The value when <see cref="Ok"/>; throws otherwise, so callers check first.</summary>
    public T Require() => Ok ? Value! : throw new InvalidOperationException($"Hevy call failed: {Error!.Message}");
}
