using A2S.Domain.Entities;
using A2S.Domain.Repositories;
using System.Security.Claims;

namespace A2S.Api.Middleware;

/// <summary>
/// Middleware that auto-provisions a User entity when a new authenticated user is detected.
/// This ensures that every authenticated user (from Clerk/Identity) has a corresponding User record
/// in our domain model.
/// </summary>
public class AutoProvisionUserMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AutoProvisionUserMiddleware> _logger;

    public AutoProvisionUserMiddleware(
        RequestDelegate next,
        ILogger<AutoProvisionUserMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IUserRepository userRepository)
    {
        // Only process authenticated requests
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var email = context.User.FindFirstValue(ClaimTypes.Email)
            ?? context.User.FindFirstValue("email");

        if (string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("Authenticated user has no email claim");
            await _next(context);
            return;
        }

        // Look up existing user
        var user = await userRepository.GetByEmailAsync(email);

        if (user is null)
        {
            // Auto-provision new user
            var name = context.User.FindFirstValue(ClaimTypes.Name)
                ?? context.User.FindFirstValue("name")
                ?? email.Split('@')[0]; // Fallback to email prefix

            try
            {
                user = User.Create(email, name);
                await userRepository.AddAsync(user);
                await userRepository.SaveChangesAsync();

                _logger.LogInformation("Auto-provisioned new user {UserId} with email {Email}", user.Id, email);
            }
            catch (Exception ex)
            {
                // Log but don't fail the request - another concurrent request may have created the user
                _logger.LogWarning(ex, "Failed to auto-provision user with email {Email}", email);

                // Try to fetch the user that was created by another request
                user = await userRepository.GetByEmailAsync(email);
            }
        }

        // Store user ID in HttpContext for downstream use
        if (user is not null)
        {
            context.Items["UserId"] = user.Id;
            context.Items["UserEmail"] = user.Email;
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering the AutoProvisionUserMiddleware.
/// </summary>
public static class AutoProvisionUserMiddlewareExtensions
{
    public static IApplicationBuilder UseAutoProvisionUser(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AutoProvisionUserMiddleware>();
    }
}
