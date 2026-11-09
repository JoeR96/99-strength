using System.Net;
using System.Text;

namespace A2S.Integration.Hevy.Tests;

/// <summary>
/// A scripted <see cref="HttpMessageHandler"/>: queue responses (or exceptions) in order and it records
/// every request it saw, so tests can assert on retries, headers and bodies. The expected key is the
/// fixture value <c>test</c>, never a real one.
/// </summary>
public sealed class FakeHevyHandler : HttpMessageHandler
{
    public const string TestApiKey = "test";

    private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> _script = new();

    public List<RecordedRequest> Requests { get; } = [];

    public FakeHevyHandler Enqueue(HttpStatusCode status, string? body = null, Action<HttpResponseMessage>? configure = null)
    {
        _script.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json")
            };
            configure?.Invoke(response);
            return Task.FromResult(response);
        });
        return this;
    }

    public FakeHevyHandler EnqueueFixture(HttpStatusCode status, string fixtureName) =>
        Enqueue(status, Fixtures.Read(fixtureName));

    public FakeHevyHandler EnqueueTimeout()
    {
        _script.Enqueue(_ => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout", new TimeoutException()));
        return this;
    }

    public FakeHevyHandler EnqueueNetworkFailure()
    {
        _script.Enqueue(_ => throw new HttpRequestException("Connection refused"));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var apiKey = request.Headers.TryGetValues("api-key", out var values) ? values.FirstOrDefault() : null;
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, apiKey, body));

        if (_script.Count == 0)
        {
            throw new InvalidOperationException($"No scripted response for {request.Method} {request.RequestUri}");
        }

        return await _script.Dequeue()(request);
    }

    public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? ApiKey, string? Body);
}

public static class Fixtures
{
    public static string Read(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        return File.ReadAllText(path);
    }
}
