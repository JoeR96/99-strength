using A2S.Application.Common;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using A2S.Domain.Repositories;

namespace A2S.Application.Commands.Habits;

/// <summary>
/// The ownership checks every habit command and query makes: an authenticated caller, an existing habit,
/// and that habit belonging to the caller.
/// </summary>
internal static class HabitAccess
{
    public static Result<UserId> RequireUser(ICurrentUserService currentUser)
    {
        var userId = currentUser.GetUserId();
        return userId is null
            ? Result.Failure<UserId>("User must be authenticated.", ErrorCode.Unauthenticated)
            : Result.Success(userId.Value);
    }

    public static async Task<Result<HabitDefinition>> LoadOwnedAsync(
        IHabitRepository habits,
        UserId userId,
        Guid habitId,
        CancellationToken ct)
    {
        var habit = await habits.GetByIdAsync(new HabitId(habitId), ct);
        if (habit is null)
        {
            return Result.Failure<HabitDefinition>("Habit not found.", ErrorCode.NotFound);
        }

        if (habit.UserId != userId)
        {
            return Result.Failure<HabitDefinition>("You can only change your own habits.", ErrorCode.Unauthorized);
        }

        return Result.Success(habit);
    }

    public static Result<T> Fail<T>(Result failed) => Result.Failure<T>(failed.Error, failed.ErrorCode);
}
