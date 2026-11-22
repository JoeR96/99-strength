using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>Repository for the CardioTrack aggregate. Every read is scoped to a user.</summary>
public interface ICardioTrackRepository : IRepository<CardioTrack, CardioTrackId>
{
    Task<IReadOnlyList<CardioTrack>> ListForUserAsync(UserId userId, CancellationToken ct = default);
}
