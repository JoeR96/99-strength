using A2S.Application.Common;

namespace A2S.Tests.Shared;

/// <summary>
/// Test implementation of ICurrentUserService.
/// Uses static fields to allow setting the current user across service instances.
/// Note: Tests should not run in parallel when using this service.
/// </summary>
public class TestCurrentUserService : ICurrentUserService
{
    // Static fields to share user context across service instances within a request
    private static string? _currentUserId;
    private static string? _currentEmail;
    private static readonly object _lock = new();

    public string? UserId => _currentUserId;
    public string? Email => _currentEmail;
    public bool IsAuthenticated => !string.IsNullOrEmpty(UserId);

    /// <summary>
    /// Sets the current user for subsequent requests.
    /// Call this at the start of each test to set up the user.
    /// </summary>
    public static void SetCurrentUser(string userId, string? email = null)
    {
        lock (_lock)
        {
            _currentUserId = userId;
            _currentEmail = email ?? $"{userId}@test.com";
        }
    }

    /// <summary>
    /// Clears the current user context.
    /// </summary>
    public static void ClearCurrentUser()
    {
        lock (_lock)
        {
            _currentUserId = null;
            _currentEmail = null;
        }
    }
}
