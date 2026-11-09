using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using A2S.Application.Interfaces.Hevy;
using A2S.Integration.Hevy.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;

namespace A2S.Integration.Hevy;

/// <summary>
/// The typed Hevy client behind <see cref="IHevyClient"/>. One <see cref="HttpClient"/> per call from the
/// factory (15 s timeout), a circuit breaker per API key, exponential retry for GETs only (a POST that
/// timed out may well have succeeded), <c>Retry-After</c> honoured on 429, and every HTTP outcome mapped
/// to a <see cref="HevyError"/>. The key goes into the <c>api-key</c> header and nowhere else: not in
/// logs, not in exceptions, not in the circuit dictionary (which is keyed by a hash).
/// </summary>
public sealed class HevyClient : IHevyClient
{
    public const string HttpClientName = "HevyTyped";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HevyOptions _options;
    private readonly ILogger<HevyClient> _logger;
    private readonly ConcurrentDictionary<string, AsyncCircuitBreakerPolicy<HttpResponseMessage>> _circuits = new();

    public HevyClient(IHttpClientFactory httpClientFactory, IOptions<HevyOptions> options, ILogger<HevyClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<HevyResult<HevyPage<HevyWorkoutEvent>>> GetWorkoutEventsAsync(string apiKey, DateTimeOffset since, int page, int pageSize = 10, CancellationToken ct = default)
    {
        var sinceText = Uri.EscapeDataString(since.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
        var path = $"workouts/events?since={sinceText}&page={Math.Max(page, 1)}&pageSize={Math.Clamp(pageSize, 1, 10)}";
        return SendAsync(apiKey, HttpMethod.Get, path, null, root => ReadPage<HevyWorkoutEvent>(root, "events"), ct);
    }

    public Task<HevyResult<HevyPage<HevyRoutine>>> GetRoutinesAsync(string apiKey, int page, CancellationToken ct = default) =>
        SendAsync(apiKey, HttpMethod.Get, $"routines?page={Math.Max(page, 1)}&pageSize=10", null, root => ReadPage<HevyRoutine>(root, "routines"), ct);

    public Task<HevyResult<HevyRoutine>> CreateRoutineAsync(string apiKey, HevyRoutineDraft routine, CancellationToken ct = default) =>
        SendAsync(apiKey, HttpMethod.Post, "routines", new Dictionary<string, object> { ["routine"] = routine }, root => ReadObject<HevyRoutine>(root, "routine"), ct);

    public Task<HevyResult<HevyRoutine>> UpdateRoutineAsync(string apiKey, string routineId, HevyRoutineDraft routine, CancellationToken ct = default) =>
        SendAsync(apiKey, HttpMethod.Put, $"routines/{Uri.EscapeDataString(routineId)}", new Dictionary<string, object> { ["routine"] = routine }, root => ReadObject<HevyRoutine>(root, "routine"), ct);

    public Task<HevyResult<HevyPage<HevyRoutineFolder>>> GetRoutineFoldersAsync(string apiKey, int page, CancellationToken ct = default) =>
        SendAsync(apiKey, HttpMethod.Get, $"routine_folders?page={Math.Max(page, 1)}&pageSize=10", null, root => ReadPage<HevyRoutineFolder>(root, "routine_folders"), ct);

    public Task<HevyResult<HevyRoutineFolder>> CreateRoutineFolderAsync(string apiKey, string title, CancellationToken ct = default) =>
        SendAsync(apiKey, HttpMethod.Post, "routine_folders", new Dictionary<string, object> { ["routine_folder"] = new Dictionary<string, object> { ["title"] = title } }, root => ReadObject<HevyRoutineFolder>(root, "routine_folder"), ct);

    public Task<HevyResult<HevyPage<HevyExerciseTemplate>>> GetExerciseTemplatesAsync(string apiKey, int page, CancellationToken ct = default) =>
        SendAsync(apiKey, HttpMethod.Get, $"exercise_templates?page={Math.Max(page, 1)}&pageSize=10", null, root => ReadPage<HevyExerciseTemplate>(root, "exercise_templates"), ct);

    public Task<HevyResult<HevyExerciseTemplate>> CreateExerciseTemplateAsync(string apiKey, HevyExerciseTemplateDraft template, CancellationToken ct = default) =>
        SendAsync(apiKey, HttpMethod.Post, "exercise_templates", new Dictionary<string, object> { ["exercise_template"] = template }, root => ReadObject<HevyExerciseTemplate>(root, "exercise_template"), ct);

    public async Task<HevyResult<HevyBodyMeasurement>> UpsertBodyMeasurementAsync(string apiKey, DateOnly date, decimal weightKg, CancellationToken ct = default)
    {
        var dateText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var body = new Dictionary<string, object>
        {
            ["body_measurement"] = new Dictionary<string, object> { ["date"] = dateText, ["weight_kg"] = weightKg }
        };

        var created = await SendAsync(apiKey, HttpMethod.Post, "body_measurements", body, root => ReadObject<HevyBodyMeasurement>(root, "body_measurement"), ct);
        if (created.Ok || created.Error!.Kind != HevyErrorKind.Conflict)
        {
            return created;
        }

        return await SendAsync(apiKey, HttpMethod.Put, $"body_measurements/{dateText}", body, root => ReadObject<HevyBodyMeasurement>(root, "body_measurement"), ct);
    }

    public async Task<HevyResult<bool>> ValidateKeyAsync(string apiKey, CancellationToken ct = default)
    {
        var result = await SendAsync(apiKey, HttpMethod.Get, "workouts/count", null, _ => true, ct);
        if (result.Ok)
        {
            return result;
        }

        return result.Error!.Kind is HevyErrorKind.InvalidKey or HevyErrorKind.ProNeeded
            ? HevyResult<bool>.Success(false)
            : result;
    }

    private async Task<HevyResult<T>> SendAsync<T>(
        string apiKey,
        HttpMethod method,
        string relativePath,
        object? body,
        Func<JsonElement, T> read,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return HevyResult<T>.Failure(HevyError.InvalidKey());
        }

        var payload = body is null ? null : JsonSerializer.Serialize(body, Json);
        var policy = PolicyFor(apiKey, method);

        HttpResponseMessage response;
        try
        {
            response = await policy.ExecuteAsync(async token =>
            {
                using var request = new HttpRequestMessage(method, $"{_options.BaseUrl.TrimEnd('/')}/{relativePath}");
                request.Headers.Add("api-key", apiKey);
                if (payload is not null)
                {
                    request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                }

                var client = _httpClientFactory.CreateClient(HttpClientName);
                return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            }, ct);
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Hevy {Method} {Path}: circuit open", method, Describe(relativePath));
            return HevyResult<T>.Failure(HevyError.Unavailable());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Hevy {Method} {Path}: network failure", method, Describe(relativePath));
            return HevyResult<T>.Failure(HevyError.Unavailable());
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Hevy {Method} {Path}: timed out", method, Describe(relativePath));
            return HevyResult<T>.Failure(HevyError.Unavailable());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Hevy {Method} {Path}: timed out", method, Describe(relativePath));
            return HevyResult<T>.Failure(HevyError.Unavailable());
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    if (stream.CanSeek && stream.Length == 0)
                    {
                        return HevyResult<T>.Success(read(default));
                    }

                    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                    return HevyResult<T>.Success(read(document.RootElement.Clone()));
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Hevy {Method} {Path}: unreadable response", method, Describe(relativePath));
                    return HevyResult<T>.Failure(HevyError.Unavailable());
                }
            }

            var error = await MapErrorAsync(response, ct);
            _logger.LogWarning("Hevy {Method} {Path} responded {StatusCode}: {Kind}", method, Describe(relativePath), (int)response.StatusCode, error.Kind);
            return HevyResult<T>.Failure(error);
        }
    }

    private IAsyncPolicy<HttpResponseMessage> PolicyFor(string apiKey, HttpMethod method)
    {
        var circuit = _circuits.GetOrAdd(HashKey(apiKey), _ => Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .Or<TaskCanceledException>()
            .OrResult(r => (int)r.StatusCode >= 500)
            .CircuitBreakerAsync(Math.Max(_options.CircuitBreakerThreshold, 1), TimeSpan.FromSeconds(Math.Max(_options.CircuitBreakerDurationSeconds, 1))));

        if (method != HttpMethod.Get)
        {
            return circuit;
        }

        var baseDelay = TimeSpan.FromMilliseconds(Math.Max(_options.RetryBaseDelayMilliseconds, 1));
        var maxRetryAfter = TimeSpan.FromSeconds(Math.Max(_options.MaxRetryAfterSeconds, 0));

        var retry = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .Or<TaskCanceledException>()
            .OrResult(r => (int)r.StatusCode >= 500)
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests && RetryAfterOf(r) is { } wait && wait <= maxRetryAfter)
            .WaitAndRetryAsync(
                Math.Max(_options.RetryCount, 0),
                (attempt, outcome, _) =>
                    outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests
                        ? RetryAfterOf(outcome.Result) ?? baseDelay
                        : TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1)),
                (outcome, _, attempt, _) =>
                {
                    outcome.Result?.Dispose();
                    _logger.LogDebug("Hevy GET retry {Attempt}", attempt);
                    return Task.CompletedTask;
                });

        return Policy.WrapAsync(retry, circuit);
    }

    private static async Task<HevyError> MapErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                return HevyError.InvalidKey();
            case HttpStatusCode.Forbidden:
                return HevyError.ProNeeded();
            case HttpStatusCode.NotFound:
                return HevyError.NotFound();
            case HttpStatusCode.Conflict:
                return HevyError.Conflict();
            case HttpStatusCode.TooManyRequests:
                return HevyError.RateLimited(RetryAfterOf(response));
            case HttpStatusCode.BadRequest:
            case HttpStatusCode.UnprocessableEntity:
                return HevyError.BadRequest(await ReadDetailAsync(response, ct));
            default:
                return HevyError.Unavailable();
        }
    }

    private static TimeSpan? RetryAfterOf(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header is null)
        {
            return null;
        }

        if (header.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (header.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
        }

        return null;
    }

    private static async Task<string?> ReadDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(text);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var name in new[] { "error", "message", "detail" })
                    {
                        if (document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                        {
                            return Truncate(value.GetString());
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Plain-text body; fall through.
            }

            return Truncate(text);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            return null;
        }
    }

    private static string? Truncate(string? text) =>
        text is null ? null : text.Length <= 200 ? text : text[..200];

    private static HevyPage<T> ReadPage<T>(JsonElement root, string itemsName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new HevyPage<T> { Page = 1, PageCount = 1, Items = [] };
        }

        var page = root.TryGetProperty("page", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 1;
        var pageCount = root.TryGetProperty("page_count", out var pc) && pc.ValueKind == JsonValueKind.Number ? pc.GetInt32() : page;
        var items = root.TryGetProperty(itemsName, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.Deserialize<List<T>>(Json) ?? []
            : [];

        return new HevyPage<T> { Page = page, PageCount = pageCount, Items = items };
    }

    /// <summary>Hevy wraps some single objects (<c>{ "routine": {...} }</c>) and returns others bare; accept both.</summary>
    private static T ReadObject<T>(JsonElement root, string wrapperName)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(wrapperName, out var inner) && inner.ValueKind == JsonValueKind.Object)
        {
            return inner.Deserialize<T>(Json)!;
        }

        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            return root[0].Deserialize<T>(Json)!;
        }

        return root.Deserialize<T>(Json)!;
    }

    private static string HashKey(string apiKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

    /// <summary>The path without its query string, for log lines.</summary>
    private static string Describe(string relativePath)
    {
        var index = relativePath.IndexOf('?');
        return index < 0 ? relativePath : relativePath[..index];
    }
}
