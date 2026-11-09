using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.SuggestedPlans;

/// <summary>
/// The suggested plans a person can start with one tap, previewed for today: start date (the coming
/// Monday), the week, the lifting days with their time estimates, the inputs needed, and whether the
/// plan is already the active one.
/// </summary>
public sealed record GetSuggestedPlansQuery : IQuery<Result<IReadOnlyList<SuggestedPlanPreviewDto>>>;

public sealed class GetSuggestedPlansQueryHandler : IRequestHandler<GetSuggestedPlansQuery, Result<IReadOnlyList<SuggestedPlanPreviewDto>>>
{
    // Estimates and the schedule do not depend on the training maxes; any valid value previews the plan.
    private const decimal PreviewTrainingMax = 100m;

    private readonly ITrainingPlanRepository _plans;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public GetSuggestedPlansQueryHandler(ITrainingPlanRepository plans, ICurrentUserService currentUser, ILocalClock clock)
    {
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<IReadOnlyList<SuggestedPlanPreviewDto>>> Handle(GetSuggestedPlansQuery request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<IReadOnlyList<SuggestedPlanPreviewDto>>(user);
        }

        var active = await _plans.GetActiveForUserAsync(user.Value, cancellationToken);
        var spec = CutTo85Nov2026.Build(new SuggestedPlanInputs(
            PreviewTrainingMax, PreviewTrainingMax, PreviewTrainingMax, PreviewTrainingMax,
            CutTo85Nov2026.DefaultStartWeightKg,
            _clock.Today()));

        var preview = new SuggestedPlanPreviewDto(
            spec.Key,
            spec.Name,
            spec.Description,
            spec.StartDate,
            CutTo85Nov2026.Inputs,
            spec.Schedule.Select(s => new SuggestedSessionPreviewDto(s.Day, s.Kind, s.Label)).ToList(),
            spec.LiftingDays.Select(d => new SuggestedDayPreviewDto(
                d.Label,
                spec.Exercises.Where(e => e.AssignedDay == d.Day).OrderBy(e => e.OrderInDay).Select(e => e.TemplateName).ToList(),
                d.Estimate.TotalMinutes,
                d.Estimate.OverBudget)).ToList(),
            spec.Habits.Select(h => h.Name).ToList(),
            spec.Checkpoints,
            AlreadyStarted: active is not null && string.Equals(active.Name, spec.Name, StringComparison.Ordinal));

        return Result.Success<IReadOnlyList<SuggestedPlanPreviewDto>>([preview]);
    }
}
