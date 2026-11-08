using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Aggregates.Plan;
using FluentValidation;

namespace A2S.Application.Commands.SuggestedPlans;

/// <summary>
/// One tap on first run: builds the "Cut to 85 — Nov 2026" plan from four training maxes and a start
/// weight. Creates the A2S2 workout, cardio protocols, mobility routines and habits, then the plan that
/// links them, in one unit of work. Idempotent per user: while that plan is the active one, starting
/// again returns it instead of creating a second plan and workout.
/// </summary>
public sealed record StartSuggestedPlanCommand(
    decimal BenchTm,
    decimal SquatTm,
    decimal OhpTm,
    decimal DeadliftTm,
    decimal StartWeightKg = CutTo85Nov2026.DefaultStartWeightKg) : ICommand<Result<StartSuggestedPlanResult>>;

/// <summary>
/// What starting the plan produced. <see cref="AlreadyStarted"/> is true when an earlier start was
/// returned unchanged. <see cref="Warnings"/> are the schedule's soft-rule warnings (none for the seed).
/// </summary>
public sealed record StartSuggestedPlanResult(
    Guid PlanId,
    Guid? WorkoutId,
    DateOnly StartDate,
    bool AlreadyStarted,
    IReadOnlyList<ScheduleWarningDto> Warnings);

public sealed class StartSuggestedPlanCommandValidator : AbstractValidator<StartSuggestedPlanCommand>
{
    public StartSuggestedPlanCommandValidator()
    {
        RuleFor(x => x.BenchTm).InclusiveBetween(CutTo85Nov2026.MinimumTrainingMaxKg, CutTo85Nov2026.MaximumTrainingMaxKg);
        RuleFor(x => x.SquatTm).InclusiveBetween(CutTo85Nov2026.MinimumTrainingMaxKg, CutTo85Nov2026.MaximumTrainingMaxKg);
        RuleFor(x => x.OhpTm).InclusiveBetween(CutTo85Nov2026.MinimumTrainingMaxKg, CutTo85Nov2026.MaximumTrainingMaxKg);
        RuleFor(x => x.DeadliftTm).InclusiveBetween(CutTo85Nov2026.MinimumTrainingMaxKg, CutTo85Nov2026.MaximumTrainingMaxKg);
        RuleFor(x => x.StartWeightKg).InclusiveBetween(TrainingPlan.MinimumStartWeightKg, TrainingPlan.MaximumStartWeightKg);
    }
}
