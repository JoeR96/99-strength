using System.Security.Cryptography;
using A2S.Domain.Common;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace A2S.Infrastructure.Security;

/// <summary>
/// <see cref="ISecretProtector"/> backed by ASP.NET Core Data Protection.
/// The key ring is persisted in the database (see <c>DependencyInjection</c>), so protected values
/// survive redeploys. A value that cannot be unprotected is reported as absent rather than thrown,
/// so a lost key ring degrades to "please re-enter your key" instead of breaking every user load.
/// </summary>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    public const string Purpose = "A2S.User.HevyApiKey";

    private readonly IDataProtector _protector;
    private readonly ILogger<DataProtectionSecretProtector> _logger;

    public DataProtectionSecretProtector(
        IDataProtectionProvider provider,
        ILogger<DataProtectionSecretProtector> logger)
    {
        _protector = provider.CreateProtector(Purpose);
        _logger = logger;
    }

    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        return _protector.Protect(plaintext);
    }

    public string? Unprotect(string protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (CryptographicException)
        {
            _logger.LogWarning("A protected secret could not be unprotected; treating it as absent.");
            return null;
        }
    }
}
