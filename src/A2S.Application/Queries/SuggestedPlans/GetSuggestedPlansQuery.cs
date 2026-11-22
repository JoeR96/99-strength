using A2S.Application.Commands.SuggestedPlans;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.SuggestedPlans;

/// <summary>
/// Every plan template (Lumbridge Lard Shed, Plan v3, …) a person can start, previewed for the coming Monday:
/// the week, the lifting days with their time estimates, the inputs, and whether it is already active.
/// </summary>
public sealed record GetSuggestedPlansQuery : IQuery<Result<IReadOnlyList<SuggestedPlanPreviewDto>>>;

/// <summary>Plan templates previewed from <paramref name="StartDate"/> (snapped to a Monday; default the coming one).</summary>
public sealed record GetPlanTemplatesQuery(DateOnly? StartDate = null, string? Key = null) : IQuery<Result<IReadOnlyList<SuggestedPlanPreviewDto>>>;

public sealed class GetSuggestedPlansQueryHandler :
    IRequestHandler<GetSuggestedPlansQuery, Result<IReadOnlyList<SuggestedPlanPreviewDto>>>,
    IRequestHandler<GetPlanTemplatesQuery, Result<IReadOnlyList<SuggestedPlanPreviewDto>>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public GetSuggestedPlansQueryHandler(ITrainingPlanRepository plans, ICurrentUserService currentUser, ILocalClock clock)
    {
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<Result<IReadOnlyList<SuggestedPlanPreviewDto>>> Handle(GetSuggestedPlansQuery request, CancellationToken cancellationToken) =>
        Handle(new GetPlanTemplatesQuery(), cancellationToken);

    public async Task<Result<IReadOnlyList<SuggestedPlanPreviewDto>>> Handle(GetPlanTemplatesQuery request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<IReadOnlyList<SuggestedPlanPreviewDto>>(user);
        }

        var templates = request.Key is null
            ? PlanTemplates.All
            : PlanTemplates.All.Where(t => string.Equals(t.Key, request.Key, StringComparison.OrdinalIgnoreCase)).ToList();
        if (request.Key is not null && templates.Count == 0)
        {
            return Result.Failure<IReadOnlyList<SuggestedPlanPreviewDto>>($"No plan template '{request.Key}'.", ErrorCode.NotFound);
        }

        var active = await _plans.GetActiveForUserAsync(user.Value, cancellationToken);
        var start = PlanTemplateInputBinder.StartDate(request.StartDate, _clock.Today());

        return Result.Success<IReadOnlyList<SuggestedPlanPreviewDto>>(templates
            .Select(t => PlanTemplatePreview.Build(t, start, active is not null && PlanTemplates.IsSamePlan(active.Name, t.Name)))
            .ToList());
    }
}
