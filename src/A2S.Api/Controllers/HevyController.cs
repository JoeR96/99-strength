using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace A2S.Api.Controllers;

/// <summary>
/// Proxy controller for Hevy API to avoid CORS issues
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class HevyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HevyController> _logger;
    private const string HevyApiBaseUrl = "https://api.hevyapp.com/v1";

    public HevyController(IHttpClientFactory httpClientFactory, ILogger<HevyController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Validate Hevy API key - must be defined before catch-all routes
    /// </summary>
    [HttpGet("validate")]
    public async Task<IActionResult> ValidateApiKey([FromHeader(Name = "X-Hevy-Api-Key")] string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return BadRequest(new { valid = false, error = "API key is required" });
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("api-key", apiKey);

            var response = await client.GetAsync($"{HevyApiBaseUrl}/workouts/count");

            _logger.LogInformation("Hevy API validation: StatusCode={StatusCode}", response.StatusCode);

            return Ok(new { valid = response.IsSuccessStatusCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Hevy API key");
            return Ok(new { valid = false, error = "Failed to validate API key" });
        }
    }

    /// <summary>
    /// Proxy GET request to Hevy API for specific paths
    /// </summary>
    [HttpGet("workouts")]
    [HttpGet("workouts/{id}")]
    [HttpGet("workouts/count")]
    [HttpGet("routines")]
    [HttpGet("routines/{id}")]
    [HttpGet("exercise_templates")]
    [HttpGet("exercise_templates/{id}")]
    [HttpGet("routine_folders")]
    [HttpGet("routine_folders/{id}")]
    public async Task<IActionResult> ProxyGet([FromHeader(Name = "X-Hevy-Api-Key")] string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return BadRequest(new { error = "Hevy API key is required" });
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("api-key", apiKey);

            // Get the path after /api/v1/hevy/
            var path = Request.Path.ToString().Replace("/api/v1/hevy/", "");
            var url = $"{HevyApiBaseUrl}/{path}{Request.QueryString}";

            _logger.LogInformation("Proxying GET to Hevy: {Url}", url);

            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = content,
                ContentType = "application/json"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying GET request to Hevy API");
            return StatusCode(500, new { error = "Failed to communicate with Hevy API" });
        }
    }

    /// <summary>
    /// Proxy POST request to Hevy API
    /// </summary>
    [HttpPost("workouts")]
    [HttpPost("routines")]
    [HttpPost("exercise_templates")]
    [HttpPost("routine_folders")]
    public async Task<IActionResult> ProxyPost([FromHeader(Name = "X-Hevy-Api-Key")] string? apiKey, [FromBody] JsonElement body)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return BadRequest(new { error = "Hevy API key is required" });
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("api-key", apiKey);

            var path = Request.Path.ToString().Replace("/api/v1/hevy/", "");
            var url = $"{HevyApiBaseUrl}/{path}";

            var bodyJson = body.GetRawText();
            _logger.LogInformation("Proxying POST to Hevy: {Url}", url);
            _logger.LogInformation("POST body: {Body}", bodyJson);

            var jsonContent = new StringContent(
                bodyJson,
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.PostAsync(url, jsonContent);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Hevy API error: {StatusCode} - {Content}", response.StatusCode, content);
            }

            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = content,
                ContentType = "application/json"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying POST request to Hevy API");
            return StatusCode(500, new { error = "Failed to communicate with Hevy API" });
        }
    }

    /// <summary>
    /// Proxy PUT request to Hevy API
    /// </summary>
    [HttpPut("workouts/{id}")]
    [HttpPut("routines/{id}")]
    public async Task<IActionResult> ProxyPut([FromHeader(Name = "X-Hevy-Api-Key")] string? apiKey, [FromBody] JsonElement body)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return BadRequest(new { error = "Hevy API key is required" });
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("api-key", apiKey);

            var path = Request.Path.ToString().Replace("/api/v1/hevy/", "");
            var url = $"{HevyApiBaseUrl}/{path}";

            _logger.LogInformation("Proxying PUT to Hevy: {Url}", url);

            var jsonContent = new StringContent(
                body.GetRawText(),
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.PutAsync(url, jsonContent);
            var content = await response.Content.ReadAsStringAsync();

            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = content,
                ContentType = "application/json"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying PUT request to Hevy API");
            return StatusCode(500, new { error = "Failed to communicate with Hevy API" });
        }
    }

    /// <summary>
    /// Proxy DELETE request to Hevy API
    /// </summary>
    [HttpDelete("routines/{id}")]
    [HttpDelete("workouts/{id}")]
    public async Task<IActionResult> ProxyDelete([FromHeader(Name = "X-Hevy-Api-Key")] string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return BadRequest(new { error = "Hevy API key is required" });
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("api-key", apiKey);

            var path = Request.Path.ToString().Replace("/api/v1/hevy/", "");
            var url = $"{HevyApiBaseUrl}/{path}";

            _logger.LogInformation("Proxying DELETE to Hevy: {Url}", url);

            var response = await client.DeleteAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = string.IsNullOrEmpty(content) ? "{}" : content,
                ContentType = "application/json"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying DELETE request to Hevy API");
            return StatusCode(500, new { error = "Failed to communicate with Hevy API" });
        }
    }
}
