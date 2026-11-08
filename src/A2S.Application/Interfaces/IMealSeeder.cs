using A2S.Domain.Common;

namespace A2S.Application.Interfaces;

/// <summary>
/// Gives a user the starter recipe set the first time they open Meals. Idempotent: meals are matched
/// by external key, so running it again inserts nothing and never touches a meal the user has edited.
/// </summary>
public interface IMealSeeder
{
    /// <summary>Returns how many meals were inserted for the user in this run.</summary>
    Task<int> EnsureForUserAsync(UserId userId, CancellationToken ct = default);
}
