using System.Text.RegularExpressions;
using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.User;

/// <summary>
/// User aggregate root — owns user identity within the domain.
/// </summary>
public sealed class User : AggregateRoot<UserId>
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Private constructor to enforce factory pattern.
    /// </summary>
    private User(UserId id, string email, string name, DateTime createdAt)
        : base(id)
    {
        Email = email;
        Name = name;
        CreatedAt = createdAt;
    }

    // EF Core constructor
    private User() { Email = string.Empty; Name = string.Empty; }

    public string Email { get; private set; }
    public string Name { get; private set; }
    public DateTime CreatedAt { get; private set; }
    /// <summary>
    /// The user's Hevy API key in protected (encrypted) form. Never the plaintext.
    /// </summary>
    public string? HevyApiKeyProtected { get; private set; }

    /// <summary>Whether the app pushes planned routines into Hevy (on by default). Finished sessions are posted either way.</summary>
    public bool PushRoutinesToHevy { get; private set; } = true;

    public void SetPushRoutinesToHevy(bool push) => PushRoutinesToHevy = push;

    /// <summary>Max heart rate in bpm, entered by hand (or from WHOOP later). Null until set.</summary>
    public int? MaxHeartRate { get; private set; }

    /// <summary>Lower bounds of zones 1–5 as % of max HR. Null means WHOOP's 50/60/70/80/90.</summary>
    public List<int>? HeartRateZonePercents { get; private set; }

    /// <summary>The person's heart-rate zones, or null without a max HR.</summary>
    public HeartRateZones? HeartRateZones => MaxHeartRate is { } max
        ? new HeartRateZones(max, HeartRateZonePercents ?? HeartRateZones.WhoopLowerPercents.ToList())
        : null;

    /// <summary>Sets max HR and (optionally) custom zone boundaries. Null max clears both.</summary>
    public void SetHeartRate(int? maxHeartRate, IReadOnlyList<int>? zoneLowerPercents)
    {
        if (maxHeartRate is null)
        {
            MaxHeartRate = null;
            HeartRateZonePercents = null;
            return;
        }

        // Validates both through the value object.
        var zones = new HeartRateZones(maxHeartRate.Value, zoneLowerPercents ?? HeartRateZones.WhoopLowerPercents);
        MaxHeartRate = zones.MaxHeartRate;
        HeartRateZonePercents = zoneLowerPercents is null ? null : zones.LowerPercents.ToList();
    }

    /// <summary>Whether a Hevy API key is stored for this user.</summary>
    public bool HasHevyApiKey => HevyApiKeyProtected is not null;

    /// <summary>
    /// Creates a new user with validation.
    /// </summary>
    public static User Create(string email, string name, UserId? id = null, DateTime? createdAt = null)
    {
        ValidateEmail(email);
        ValidateName(name);

        return new User(
            id: id ?? new UserId(Guid.NewGuid().ToString()),
            email: email.Trim().ToLowerInvariant(),
            name: name.Trim(),
            createdAt: createdAt ?? DateTime.UtcNow);
    }

    /// <summary>
    /// Reconstitutes a user from persistence.
    /// Used by EF Core and for testing.
    /// </summary>
    internal static User Reconstitute(string id, string email, string name, DateTime createdAt)
    {
        return new User(new UserId(id), email, name, createdAt);
    }

    private static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));
        }

        if (!EmailRegex.IsMatch(email))
        {
            throw new ArgumentException("Email is not in a valid format.", nameof(email));
        }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be null or empty.", nameof(name));
        }
    }

    /// <summary>
    /// Updates the user's name.
    /// </summary>
    public void UpdateName(string name)
    {
        ValidateName(name);
        Name = name.Trim();
    }

    /// <summary>
    /// Stores the user's Hevy API key in protected form. A blank key clears it.
    /// </summary>
    public void SetHevyApiKey(string? apiKey, ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            HevyApiKeyProtected = null;
            return;
        }

        HevyApiKeyProtected = protector.Protect(apiKey.Trim());
    }

    /// <summary>Removes the stored Hevy API key.</summary>
    public void ClearHevyApiKey()
    {
        HevyApiKeyProtected = null;
    }

    /// <summary>
    /// Returns the plaintext Hevy API key, or null when none is stored or it can no longer be unprotected.
    /// Callers must use the value immediately and never log or persist it.
    /// </summary>
    public string? RevealHevyApiKey(ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);
        return HevyApiKeyProtected is null ? null : protector.Unprotect(HevyApiKeyProtected);
    }
}
