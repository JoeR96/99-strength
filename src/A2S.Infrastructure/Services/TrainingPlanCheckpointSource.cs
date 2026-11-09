using A2S.Application.Interfaces;
using A2S.Domain.Common;
using A2S.Domain.Repositories;

namespace A2S.Infrastructure.Services;

/// <summary>
/// Feeds the weight-trend pace reading from the user's active TrainingPlan: its start date and
/// start weight plus the ordered checkpoints. No active plan means no target and <c>Pace.NoTarget</c>.
/// </summary>
public sealed class TrainingPlanCheckpointSource : IPlanCheckpointSource
{
    private readonly ITrainingPlanRepository _plans;

    public TrainingPlanCheckpointSource(ITrainingPlanRepository plans)
    {
        _plans = plans;
    }

    public async Task<PlanCheckpoints?> ForUserAsync(UserId userId, CancellationToken ct = default)
    {
        var plan = await _plans.GetActiveForUserAsync(userId, ct);
        if (plan is null)
        {
            return null;
        }

        var checkpoints = plan.Checkpoints
            .OrderBy(c => c.Date)
            .Select(c => (c.Date, c.TargetKg))
            .ToList();

        return new PlanCheckpoints(plan.StartDate, plan.StartWeightKg, checkpoints);
    }
}
