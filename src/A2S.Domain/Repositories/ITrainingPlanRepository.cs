using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;

namespace A2S.Domain.Repositories;

/// <summary>Repository for the TrainingPlan aggregate. Every read is scoped to a user.</summary>
public interface ITrainingPlanRepository : IRepository<TrainingPlan, TrainingPlanId>
{
    /// <summary>The user's active plan, with its completions loaded, or null.</summary>
    Task<TrainingPlan?> GetActiveForUserAsync(UserId userId, CancellationToken ct = default);

    Task<IReadOnlyList<TrainingPlan>> ListForUserAsync(UserId userId, CancellationToken ct = default);
}
