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
}
