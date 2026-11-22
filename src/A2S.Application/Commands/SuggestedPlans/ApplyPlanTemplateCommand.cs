using A2S.Application.Common;
using A2S.Application.Services;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.SuggestedPlans;

/// <summary>
/// Applies a plan template to the signed-in user's account. The start date snaps to the Monday on or
/// after it (Europe/London; default the coming Monday; never in the past). Training maxes are keyed by
/// the template's input keys; an optional one left empty starts pending. Replacing another active plan
/// needs <see cref="ConfirmReplace"/>; applying the active template again returns it (idempotent).
/// </summary>
public sealed record ApplyPlanTemplateCommand(
    string Key,
    DateOnly? StartDate,
    IReadOnlyDictionary<string, decimal?>? TrainingMaxes,
    decimal? StartWeightKg,
    bool ConfirmReplace) : ICommand<Result<StartSuggestedPlanResult>>;

public sealed class ApplyPlanTemplateCommandHandler : IRequestHandler<ApplyPlanTemplateCommand, Result<StartSuggestedPlanResult>>
{
    private readonly PlanTemplateApplier _applier;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public ApplyPlanTemplateCommandHandler(
        ITrainingPlanRepository plans,
        IWorkoutRepository workouts,
        ICardioProtocolRepository protocols,
        IMobilityRoutineRepository routines,
        IHabitRepository habits,
        IExerciseLibraryProvider library,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILocalClock clock,
        ICardioTrackRepository? tracks = null)
    {
        _applier = new PlanTemplateApplier(plans, workouts, protocols, routines, habits, library, unitOfWork, tracks);
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<StartSuggestedPlanResult>> Handle(ApplyPlanTemplateCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<StartSuggestedPlanResult>(user);
        }

        var template = PlanTemplates.Find(request.Key);
        if (template is null)
        {
            return Result.Failure<StartSuggestedPlanResult>($"No plan template '{request.Key}'.", ErrorCode.NotFound);
        }

        var inputs = PlanTemplateInputBinder.Bind(template, request.TrainingMaxes, request.StartWeightKg, request.StartDate, _clock.Today());
        if (inputs.IsFailure)
        {
            return Result.Failure<StartSuggestedPlanResult>(inputs.Error, ErrorCode.ValidationFailed);
        }

        return await _applier.ApplyAsync(user.Value, template.Build(inputs.Value), request.ConfirmReplace, cancellationToken);
    }
}

/// <summary>Checks a template's inputs (required present, all within range) and snaps the start date.</summary>
public static class PlanTemplateInputBinder
{
    public static DateOnly StartDate(DateOnly? requested, DateOnly today) =>
        LocalDates.ComingMonday(requested is { } date && date > today ? date : today);

    public static Result<PlanTemplateInputs> Bind(
        IPlanTemplate template, IReadOnlyDictionary<string, decimal?>? values, decimal? startWeightKg, DateOnly? startDate, DateOnly today)
    {
        var bound = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
        decimal startWeight = template.DefaultStartWeightKg;
        foreach (var input in template.Inputs)
        {
            decimal? value = input.Key == "startWeightKg"
                ? startWeightKg ?? input.Default
                : values is not null && values.TryGetValue(input.Key, out var given) ? given : null;

            if (value is null)
            {
                if (!input.Optional && input.Default is null)
                {
                    return Result.Failure<PlanTemplateInputs>($"{input.Label} is required.");
                }

                bound[input.Key] = null;
                continue;
            }

            if (value < input.Minimum || value > input.Maximum)
            {
                return Result.Failure<PlanTemplateInputs>($"{input.Label} must be between {input.Minimum:0.#} and {input.Maximum:0.#} {input.Unit}.");
            }

            bound[input.Key] = value;
            if (input.Key == "startWeightKg")
            {
                startWeight = value.Value;
            }
        }

        return Result.Success(new PlanTemplateInputs(bound, startWeight, StartDate(startDate, today)));
    }
}
