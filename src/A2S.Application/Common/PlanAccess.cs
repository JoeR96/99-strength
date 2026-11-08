using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;

namespace A2S.Application.Common;

/// <summary>
/// The user-scoping and error-mapping every plan handler shares: who is calling, which plan they
/// own, and how a domain rule failure becomes a <see cref="Result"/>. Keeps the handlers thin.
/// </summary>
public static class PlanAccess
{
    public static Result<UserId> RequireUser(ICurrentUserService currentUser)
    {
        var userId = currentUser.GetUserId();
        return userId is null
            ? Result.Failure<UserId>("User must be authenticated.", ErrorCode.Unauthenticated)
            : Result.Success(userId.Value);
    }

    /// <summary>Loads a plan the user owns. Another user's plan is reported as not found, not forbidden.</summary>
    public static async Task<Result<TrainingPlan>> LoadOwnedAsync(
        ITrainingPlanRepository plans,
        UserId userId,
        Guid planId,
        CancellationToken ct)
    {
        var plan = await plans.GetByIdAsync(new TrainingPlanId(planId), ct);
        return plan is null || plan.UserId != userId
            ? Result.Failure<TrainingPlan>("Training plan not found.", ErrorCode.NotFound)
            : Result.Success(plan);
    }

    public static async Task<Result<TrainingPlan>> LoadActiveAsync(
        ITrainingPlanRepository plans,
        UserId userId,
        CancellationToken ct)
    {
        var plan = await plans.GetActiveForUserAsync(userId, ct);
        return plan is null || plan.UserId != userId
            ? Result.Failure<TrainingPlan>("No active training plan.", ErrorCode.NotFound)
            : Result.Success(plan);
    }

    /// <summary>The linked workout's completed days, or none when no workout is linked or it is gone.</summary>
    public static async Task<IReadOnlyCollection<WorkoutActivity>> LiftingHistoryAsync(
        IWorkoutRepository workouts,
        TrainingPlan plan,
        CancellationToken ct)
    {
        if (plan.LinkedWorkoutId is not { } workoutId)
        {
            return Array.Empty<WorkoutActivity>();
        }

        var workout = await workouts.GetByIdAsync(workoutId, ct);
        return workout?.CompletedActivities ?? (IReadOnlyCollection<WorkoutActivity>)Array.Empty<WorkoutActivity>();
    }

    public static Result<T> Fail<T>(Result failure) => Result.Failure<T>(failure.Error, failure.ErrorCode);

    public static Result<T> FromDomain<T>(DomainException exception) =>
        Result.Failure<T>(exception.Message, ErrorCode.DomainRuleViolation);
}
