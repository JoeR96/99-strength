using A2S.Application.Common;
using System.Security.Claims;

namespace A2S.Api.Services;

/// <summary>
/// Implementation of ICurrentUserService that retrieves user information from HttpContext.
/// First tries to get user info from HttpContext.Items (set by AutoProvisionUserMiddleware),
/// then falls back to JWT claims for authenticated users without auto-provisioning.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            // First try HttpContext.Items (set by AutoProvisionUserMiddleware)
            if (httpContext.Items.TryGetValue("UserId", out var userId) && userId != null)
            {
                return userId.ToString();
            }

            // Clerk uses 'sub' claim for user ID
            var sub = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? httpContext.User.FindFirstValue("sub");

            return sub;
        }
    }

    public string? Email
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            // First try HttpContext.Items (set by AutoProvisionUserMiddleware)
            if (httpContext.Items.TryGetValue("UserEmail", out var email) && email != null)
            {
                return email.ToString();
            }

            return httpContext.User.FindFirstValue(ClaimTypes.Email)
                ?? httpContext.User.FindFirstValue("email");
        }
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(UserId);

    public string? HevyApiKey
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            if (httpContext.Items.TryGetValue("HevyApiKey", out var key) && key is string apiKey)
            {
                return apiKey;
            }

            return null;
        }
    }
}
