using A2S.Domain.Entities;
using A2S.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace A2S.Api.Middleware;

/// <summary>
/// Middleware that auto-provisions a User entity when a new authenticated user is detected.
/// Handles race conditions from concurrent first requests for the same user.
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

    public async Task InvokeAsync(HttpContext context, IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
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

        var user = await userRepository.GetByEmailAsync(email);

        if (user is null)
        {
            var name = context.User.FindFirstValue(ClaimTypes.Name)
                ?? context.User.FindFirstValue("name")
                ?? email.Split('@')[0];

            try
            {
                user = User.Create(email, name);
                await userRepository.AddAsync(user);
                await unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Auto-provisioned new user {UserId} with email {Email}", user.Id, email);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Concurrent user creation detected for email {Email}, fetching existing user", email);

                // Another request created this user concurrently — re-fetch
                user = await userRepository.GetByEmailAsync(email);

                if (user is null)
                {
                    _logger.LogError("Failed to provision or retrieve user for email {Email}", email);
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    return;
                }
            }
        }

        context.Items["UserId"] = user.Id.Value;
        context.Items["UserEmail"] = user.Email;

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
