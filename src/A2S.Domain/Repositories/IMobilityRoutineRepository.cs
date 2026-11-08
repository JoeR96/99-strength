using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>Repository for the MobilityRoutine aggregate. Every read is scoped to a user.</summary>
public interface IMobilityRoutineRepository : IRepository<MobilityRoutine, MobilityRoutineId>
{
    Task<IReadOnlyList<MobilityRoutine>> ListForUserAsync(UserId userId, CancellationToken ct = default);
}
