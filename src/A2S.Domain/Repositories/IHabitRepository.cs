using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>
/// Habit definitions and their per-day logs for one user.
/// </summary>
public interface IHabitRepository : IRepository<HabitDefinition, HabitId>
{
    /// <summary>The user's habits ordered by <see cref="HabitDefinition.Order"/>, archived ones only when asked.</summary>
    Task<IReadOnlyList<HabitDefinition>> ListForUserAsync(UserId userId, bool includeArchived, CancellationToken ct = default);

    /// <summary>All of the user's logs with <c>from &lt;= Date &lt;= to</c>.</summary>
    Task<IReadOnlyList<HabitLog>> GetLogsAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>The log for one habit on one date, or null.</summary>
    Task<HabitLog?> GetLogAsync(HabitId habitId, DateOnly date, CancellationToken ct = default);

    Task AddLogAsync(HabitLog log, CancellationToken ct = default);

    void UpdateLog(HabitLog log);
}
