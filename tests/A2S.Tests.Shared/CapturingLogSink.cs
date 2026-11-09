using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace A2S.Tests.Shared;

/// <summary>
/// Serilog sink that keeps every log event in memory so tests can prove that a value
/// (for example a Hevy API key) never appears in any message, property or exception.
/// Registered in DI; Program.cs picks it up through <c>ReadFrom.Services</c>.
/// </summary>
public sealed class CapturingLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyCollection<LogEvent> Events => _events.ToArray();

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    public void Clear() => _events.Clear();

    /// <summary>Every piece of text an event could carry, flattened, for substring assertions.</summary>
    public IEnumerable<string> AllText()
    {
        foreach (var e in Events)
        {
            yield return e.RenderMessage();
            yield return e.MessageTemplate.Text;
            foreach (var p in e.Properties)
            {
                yield return p.Value.ToString();
            }
            if (e.Exception is not null)
            {
                yield return e.Exception.ToString();
            }
        }
    }
}
