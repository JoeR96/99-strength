using System.Text.RegularExpressions;

namespace A2S.Domain.Entities;

/// <summary>
/// Represents a user in the system.
/// This is separate from ApplicationUser (Identity) to maintain clean domain boundaries.
/// </summary>
public class User
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Private constructor to enforce factory pattern.
    /// </summary>
    private User(Guid id, string email, string name, DateTime createdAt)
    {
        Id = id;
        Email = email;
        Name = name;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string Name { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Creates a new user with validation.
    /// </summary>
    /// <param name="email">User's email address. Must be a valid email format.</param>
    /// <param name="name">User's display name. Must not be null or empty.</param>
    /// <returns>A new User instance.</returns>
    /// <exception cref="ArgumentException">Thrown when email or name is invalid.</exception>
    public static User Create(string email, string name)
    {
        ValidateEmail(email);
        ValidateName(name);

        return new User(
            id: Guid.NewGuid(),
            email: email.Trim().ToLowerInvariant(),
            name: name.Trim(),
            createdAt: DateTime.UtcNow);
    }

    /// <summary>
    /// Reconstitutes a user from persistence.
    /// Used by EF Core and for testing.
    /// </summary>
    internal static User Reconstitute(Guid id, string email, string name, DateTime createdAt)
    {
        return new User(id, email, name, createdAt);
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
