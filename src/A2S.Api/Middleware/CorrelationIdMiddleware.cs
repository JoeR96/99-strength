using Serilog.Context;

namespace A2S.Api.Middleware;

/// <summary>
/// Middleware that generates or extracts a correlation ID from request headers
/// and adds it to the Serilog context and response headers.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Try to get correlation ID from request header, or generate a new one
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                            ?? Guid.NewGuid().ToString();

        // Add correlation ID to response headers
        context.Response.Headers.Append(CorrelationIdHeader, correlationId);

        // Add correlation ID to Serilog context so it appears in all logs
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
