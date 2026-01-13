namespace A2S.E2ETests;

/// <summary>
/// Test credentials for E2E testing.
/// In production, these would be loaded from environment variables or secure configuration.
/// </summary>
public static class TestCredentials
{
    /// <summary>
    /// Test user email for Clerk authentication.
    /// Note: Email must match the exact case used in Clerk.
    /// </summary>
    public static string Email => "Bigdave@gmail.com";

    /// <summary>
    /// Test user password for Clerk authentication.
    /// </summary>
    public static string Password => "Tacomuncher123!";

    /// <summary>
    /// Generates a random email for registration tests.
    /// </summary>
    public static string GenerateRandomEmail()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random = Guid.NewGuid().ToString("N")[..8];
        return $"test-{timestamp}-{random}@example.com";
    }

    /// <summary>
    /// Generates a random password that meets Clerk's requirements.
    /// </summary>
    public static string GenerateRandomPassword()
    {
        var random = Guid.NewGuid().ToString("N")[..12];
        return $"Test{random}123!";
    }
}
