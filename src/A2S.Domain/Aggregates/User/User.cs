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
}
