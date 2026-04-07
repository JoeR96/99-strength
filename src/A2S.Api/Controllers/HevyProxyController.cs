using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Frozen;
using System.Text;
using System.Text.Json;

namespace A2S.Api.Controllers;

/// <summary>
/// Typed proxy endpoints for Hevy API.
/// Eliminates open proxy SSRF risk by only proxying explicitly allowed paths.
/// </summary>
[ApiController]
[Route("api/v1/hevy")]
[Authorize]
public class HevyProxyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HevyProxyController> _logger;
    private const string HevyApiBaseUrl = "https://api.hevyapp.com/v1";

    /// <summary>
    /// Allowed Hevy API resource paths. Only these exact resource types can be proxied.
    /// </summary>
    private static readonly FrozenSet<string> AllowedResources = new[]
    {
        "workouts", "routines", "exercise_templates", "routine_folders"
    }.ToFrozenSet();

    /// <summary>
    /// Allowed query parameters for Hevy API requests.
    /// </summary>
    private static readonly FrozenSet<string> AllowedQueryParams = new[]
    {
        "page", "page_size", "offset"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public HevyProxyController(IHttpClientFactory httpClientFactory, ILogger<HevyProxyController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Validate Hevy API key.
    /// </summary>
    [HttpGet("validate")]
    public async Task<IActionResult> ValidateApiKey(
        [FromHeader(Name = "X-Hevy-Api-Key")] string? apiKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Failed",
                Detail = "API key is required."
            });
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{HevyApiBaseUrl}/workouts/count");
        request.Headers.Add("api-key", apiKey);

        var client = _httpClientFactory.CreateClient("HevyApi");
        using var response = await client.SendAsync(request, cancellationToken);

        return Ok(new { valid = response.IsSuccessStatusCode });
    }

    /// <summary>
    /// Proxy GET request to an allowed Hevy API resource path.
    /// </summary>
    [HttpGet("{resource}")]
    [HttpGet("{resource}/{resourceId}")]
    [HttpGet("{resource}/count")]
    public async Task<IActionResult> ProxyGet(
        [FromRoute] string resource,
        [FromRoute] string? resourceId,
        [FromHeader(Name = "X-Hevy-Api-Key")] string? apiKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return RequireApiKey();
        }

        if (!AllowedResources.Contains(resource))
        {
            return ForbiddenResource(resource);
        }

        var path = BuildPath(resource, resourceId);
        var sanitizedQuery = BuildSanitizedQueryString(Request.Query);
        var url = $"{HevyApiBaseUrl}/{path}{sanitizedQuery}";

        _logger.LogInformation("Proxying GET to Hevy: {Resource}", resource);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("api-key", apiKey);

        var client = _httpClientFactory.CreateClient("HevyApi");
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation("Hevy GET {Resource} responded with {StatusCode}", resource, (int)response.StatusCode);

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = content,
            ContentType = "application/json"
        };
    }

    /// <summary>
    /// Proxy POST request to an allowed Hevy API resource.
    /// </summary>
    [HttpPost("{resource}")]
    public async Task<IActionResult> ProxyPost(
        [FromRoute] string resource,
        [FromHeader(Name = "X-Hevy-Api-Key")] string? apiKey,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return RequireApiKey();
        }

        if (!AllowedResources.Contains(resource))
        {
            return ForbiddenResource(resource);
        }

        var url = $"{HevyApiBaseUrl}/{resource}";

        _logger.LogInformation("Proxying POST to Hevy: {Resource}", resource);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("api-key", apiKey);
        request.Content = new StringContent(body.GetRawText(), Encoding.UTF8, "application/json");

        var client = _httpClientFactory.CreateClient("HevyApi");
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation("Hevy POST {Resource} responded with {StatusCode}", resource, (int)response.StatusCode);

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = content,
            ContentType = "application/json"
        };
    }

    /// <summary>
    /// Proxy PUT request to an allowed Hevy API resource.
    /// </summary>
    [HttpPut("{resource}/{resourceId}")]
    public async Task<IActionResult> ProxyPut(
        [FromRoute] string resource,
        [FromRoute] string resourceId,
        [FromHeader(Name = "X-Hevy-Api-Key")] string? apiKey,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return RequireApiKey();
        }

        if (!AllowedResources.Contains(resource))
        {
            return ForbiddenResource(resource);
        }

        var url = $"{HevyApiBaseUrl}/{resource}/{resourceId}";

        _logger.LogInformation("Proxying PUT to Hevy: {Resource}/{ResourceId}", resource, resourceId);

        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Add("api-key", apiKey);
        request.Content = new StringContent(body.GetRawText(), Encoding.UTF8, "application/json");

        var client = _httpClientFactory.CreateClient("HevyApi");
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation("Hevy PUT {Resource}/{ResourceId} responded with {StatusCode}", resource, resourceId, (int)response.StatusCode);

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = content,
            ContentType = "application/json"
        };
    }

    /// <summary>
    /// Proxy DELETE request to an allowed Hevy API resource.
    /// </summary>
    [HttpDelete("{resource}/{resourceId}")]
    public async Task<IActionResult> ProxyDelete(
        [FromRoute] string resource,
        [FromRoute] string resourceId,
        [FromHeader(Name = "X-Hevy-Api-Key")] string? apiKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return RequireApiKey();
        }

        if (!AllowedResources.Contains(resource))
        {
            return ForbiddenResource(resource);
        }

        var url = $"{HevyApiBaseUrl}/{resource}/{resourceId}";

        _logger.LogInformation("Proxying DELETE to Hevy: {Resource}/{ResourceId}", resource, resourceId);

        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Add("api-key", apiKey);

        var client = _httpClientFactory.CreateClient("HevyApi");
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation("Hevy DELETE {Resource}/{ResourceId} responded with {StatusCode}", resource, resourceId, (int)response.StatusCode);

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = string.IsNullOrEmpty(content) ? "{}" : content,
            ContentType = "application/json"
        };
    }

    private static string BuildPath(string resource, string? resourceId)
    {
        return string.IsNullOrEmpty(resourceId) ? resource : $"{resource}/{resourceId}";
    }

    private static string BuildSanitizedQueryString(IQueryCollection query)
    {
        var allowed = query
            .Where(kvp => AllowedQueryParams.Contains(kvp.Key))
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value.ToString())}")
            .ToList();

        return allowed.Count > 0 ? $"?{string.Join("&", allowed)}" : string.Empty;
    }

    private static ActionResult RequireApiKey() => new ObjectResult(new ProblemDetails
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Validation Failed",
        Detail = "Hevy API key is required."
    })
    { StatusCode = StatusCodes.Status400BadRequest };

    private static ActionResult ForbiddenResource(string resource) => new ObjectResult(new ProblemDetails
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid Resource",
        Detail = $"Resource '{resource}' is not a supported Hevy API resource."
    })
    { StatusCode = StatusCodes.Status400BadRequest };
}
