namespace A2S.Integration.Hevy.Configuration;

/// <summary>
/// Configuration options for the Hevy API integration.
/// </summary>
public sealed class HevyOptions
{
    public const string SectionName = "Hevy";

    /// <summary>
    /// Base URL for the Hevy API.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.hevyapp.com/v1";

    /// <summary>
    /// Number of retry attempts for transient failures.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Circuit breaker failure threshold before opening the circuit.
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Duration in seconds to keep the circuit breaker open.
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    /// <summary>Per-request timeout for the typed <see cref="HevyClient"/>.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Base of the exponential back-off between GET retries (1 s, 2 s, 4 s by default).
    /// Tests set it to a millisecond.
    /// </summary>
    public int RetryBaseDelayMilliseconds { get; set; } = 1000;

    /// <summary>
    /// A 429 whose <c>Retry-After</c> is at most this many seconds is waited out and retried (GETs only);
    /// a longer one is reported to the caller as rate limited.
    /// </summary>
    public int MaxRetryAfterSeconds { get; set; } = 5;

    /// <summary>How often the background poll pulls Hevy events for users with a session today.</summary>
    public int PollMinutes { get; set; } = 15;
}
