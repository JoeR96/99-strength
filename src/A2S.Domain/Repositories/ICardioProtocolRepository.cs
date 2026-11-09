using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>Repository for the CardioProtocol aggregate. Every read is scoped to a user.</summary>
public interface ICardioProtocolRepository : IRepository<CardioProtocol, CardioProtocolId>
{
    Task<IReadOnlyList<CardioProtocol>> ListForUserAsync(UserId userId, CancellationToken ct = default);
}
