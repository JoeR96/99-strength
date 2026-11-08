using A2S.Application.Interfaces;
using A2S.Domain.Common;

namespace A2S.Infrastructure.Services;

/// <summary>
/// Stand-in until the TrainingPlan aggregate lands: no plan, so no checkpoints and no pace reading.
/// The integrator replaces this registration with a TrainingPlan-backed source.
/// </summary>
public sealed class NullPlanCheckpointSource : IPlanCheckpointSource
{
    public Task<PlanCheckpoints?> ForUserAsync(UserId userId, CancellationToken ct = default) =>
        Task.FromResult<PlanCheckpoints?>(null);
}
