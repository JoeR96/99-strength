using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>The phase-change nudge schedules of live finisher wizards.</summary>
public interface IFinisherScheduleRepository : IRepository<FinisherSchedule, FinisherScheduleId>
{
    /// <summary>The user's schedule for one gym session, or null.</summary>
    Task<FinisherSchedule?> GetAsync(UserId userId, string clientSessionId, CancellationToken ct = default);

    /// <summary>Every schedule with an unsent nudge due at or before <paramref name="nowUtc"/>.</summary>
    Task<IReadOnlyList<FinisherSchedule>> ListDueAsync(DateTimeOffset nowUtc, CancellationToken ct = default);
}
