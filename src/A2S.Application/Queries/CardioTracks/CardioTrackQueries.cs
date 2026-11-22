using A2S.Application.Commands.CardioTracks;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using MediatR;

namespace A2S.Application.Queries.CardioTracks;

/// <summary>The user's cardio tracks with today's prescription for each.</summary>
public sealed record ListCardioTracksQuery : IQuery<Result<IReadOnlyList<CardioTrackDto>>>;

public sealed class ListCardioTracksQueryHandler : IRequestHandler<ListCardioTracksQuery, Result<IReadOnlyList<CardioTrackDto>>>
{
    private readonly ICardioTrackRepository _tracks;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public ListCardioTracksQueryHandler(ICardioTrackRepository tracks, ICurrentUserService currentUser, ILocalClock clock)
    {
        _tracks = tracks;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<CardioTrackDto>>> Handle(ListCardioTracksQuery request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<IReadOnlyList<CardioTrackDto>>(user);
        }

        var today = _clock.Today();
        var tracks = await _tracks.ListForUserAsync(user.Value, cancellationToken);
        return Result.Success<IReadOnlyList<CardioTrackDto>>(tracks.Select(t => t.ToDto(today)).ToList());
    }
}

/// <summary>One track with its sessions, audit trail (each step and why), benchmarks and chart data.</summary>
public sealed record GetCardioTrackQuery(Guid Id) : IQuery<Result<CardioTrackDetailDto>>;

public sealed class GetCardioTrackQueryHandler : IRequestHandler<GetCardioTrackQuery, Result<CardioTrackDetailDto>>
{
    private readonly ICardioTrackRepository _tracks;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public GetCardioTrackQueryHandler(ICardioTrackRepository tracks, ICurrentUserService currentUser, ILocalClock clock)
    {
        _tracks = tracks;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<CardioTrackDetailDto>> Handle(GetCardioTrackQuery request, CancellationToken cancellationToken)
    {
        var load = await TrackAccess.LoadOwnedAsync(_tracks, _currentUser, request.Id, cancellationToken);
        return load.IsFailure
            ? PlanAccess.Fail<CardioTrackDetailDto>(load)
            : Result.Success(load.Value.ToDetailDto(_clock.Today()));
    }
}

/// <summary>The shipped templates a track can be started from.</summary>
public sealed record ListCardioTrackTemplatesQuery : IQuery<Result<IReadOnlyList<CardioTrackTemplateDto>>>;

public sealed class ListCardioTrackTemplatesQueryHandler : IRequestHandler<ListCardioTrackTemplatesQuery, Result<IReadOnlyList<CardioTrackTemplateDto>>>
{
    public Task<Result<IReadOnlyList<CardioTrackTemplateDto>>> Handle(ListCardioTrackTemplatesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success<IReadOnlyList<CardioTrackTemplateDto>>(
            CardioTrackTemplates.All.Select(t => new CardioTrackTemplateDto(t.Key, t.Name, t.Modality, t.Description)).ToList()));
}

/// <summary>The user's max HR and zones (WHOOP defaults until edited).</summary>
public sealed record GetHeartRateZonesQuery : IQuery<Result<HeartRateZonesDto>>;

public sealed class GetHeartRateZonesQueryHandler : IRequestHandler<GetHeartRateZonesQuery, Result<HeartRateZonesDto>>
{
    private readonly IUserRepository _users;
    private readonly ICurrentUserService _currentUser;

    public GetHeartRateZonesQueryHandler(IUserRepository users, ICurrentUserService currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    public async Task<Result<HeartRateZonesDto>> Handle(GetHeartRateZonesQuery request, CancellationToken cancellationToken)
    {
        var userId = PlanAccess.RequireUser(_currentUser);
        if (userId.IsFailure)
        {
            return PlanAccess.Fail<HeartRateZonesDto>(userId);
        }

        var user = await _users.GetByIdAsync(userId.Value, cancellationToken);
        return Result.Success(CardioTrackMappings.ToZonesDto(user?.MaxHeartRate, user?.HeartRateZonePercents));
    }
}

/// <summary>
/// Today's cardio: the prescription for every active track (with "N more sessions to step up"),
/// this week's zone 4–5 minutes, and guardrail warnings. Warnings never block anything.
/// </summary>
public sealed record GetTodayCardioQuery : IQuery<Result<TodayCardioDto>>;

public sealed class GetTodayCardioQueryHandler : IRequestHandler<GetTodayCardioQuery, Result<TodayCardioDto>>
{
    private readonly ICardioTrackRepository _tracks;
    private readonly ITrainingPlanRepository _plans;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public GetTodayCardioQueryHandler(ICardioTrackRepository tracks, ITrainingPlanRepository plans, ICurrentUserService currentUser, ILocalClock clock)
    {
        _tracks = tracks;
        _plans = plans;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<TodayCardioDto>> Handle(GetTodayCardioQuery request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<TodayCardioDto>(user);
        }

        var today = _clock.Today();
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var tracks = await _tracks.ListForUserAsync(user.Value, cancellationToken);
        var plan = await _plans.GetActiveForUserAsync(user.Value, cancellationToken);

        var minutes = CardioWeek.HighIntensityMinutes(plan, tracks, monday, today);
        var warnings = new List<CardioGuardrailDto>();
        if (PlanScheduleRules.HighIntensityOverMax(minutes, plan?.HighIntensityTarget ?? HighIntensityTarget.Default) is { } over)
        {
            warnings.Add(new CardioGuardrailDto(over.Code, over.Message));
        }

        var active = tracks.Where(t => t.Status == CardioTrackStatus.Active).Select(t => t.ToDto(today)).ToList();
        return Result.Success(new TodayCardioDto(active, minutes, warnings));
    }
}

/// <summary>Counts a week's zone 4–5 minutes once: plan completions plus track sessions that have no completion.</summary>
public static class CardioWeek
{
    public static int HighIntensityMinutes(TrainingPlan? plan, IEnumerable<CardioTrack> tracks, DateOnly from, DateOnly to)
    {
        var fromPlan = plan?.Completions
            .Where(c => c.Date >= from && c.Date <= to && c.Cardio?.HighIntensityMinutes is not null)
            .Sum(c => c.Cardio!.HighIntensityMinutes!.Value) ?? 0;

        var fromTracks = tracks
            .SelectMany(t => t.Sessions)
            .Where(s => s.Date >= from && s.Date <= to && s.HighIntensityMinutes is not null
                && !s.Key.StartsWith("completion:", StringComparison.Ordinal))
            .Sum(s => s.HighIntensityMinutes!.Value);

        return fromPlan + fromTracks;
    }
}
