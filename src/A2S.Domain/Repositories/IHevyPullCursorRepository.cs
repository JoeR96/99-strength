using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>One pull cursor per user (the aggregate id is the user id).</summary>
public interface IHevyPullCursorRepository : IRepository<HevyPullCursor, UserId>
{
}
