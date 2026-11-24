using A2S.Application.Cardio;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Hevy;
using A2S.Application.Queries.GetWeekPlan;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using MediatR;

namespace A2S.Application.Queries.Plans;

/// <summary>
/// The next <paramref name="Days"/> days of the active plan from <paramref name="From"/> (today by default),
/// with session statuses. Each day carries the planned sets of its lifts still to do, from the
/// linked workout's current A2S2 week (weeks advance on completion, not by date), so a reader needs
/// no <c>/workouts</c> call. Today's "Up next" card and Terran CC (personal API token) read it.
/// </summary>
public sealed record GetUpcomingDaysQuery(DateOnly? From, int Days = 14) : IQuery<Result<IReadOnlyList<UpcomingDayDto>>>;

public sealed class GetUpcomingDaysQueryHandler : IRequestHandler<GetUpcomingDaysQuery, Result<IReadOnlyList<UpcomingDayDto>>>
{
    public const int MaxDays = 31;

    private readonly ITrainingPlanRepository _plans;
    private readonly IWorkoutRepository _workouts;
    private readonly IHevyLinkRepository _hevyLinks;
    private readonly ICardioProtocolRepository _protocols;
    private readonly ICardioTrackRepository _tracks;
    private readonly ICurrentUserService _currentUser;
    private readonly PlanCalendarService _calendar;
    private readonly ILocalClock _clock;
    private readonly ISender _sender;

    public GetUpcomingDaysQueryHandler(
        ITrainingPlanRepository plans,
        IWorkoutRepository workouts,
        IHevyLinkRepository hevyLinks,
        ICardioProtocolRepository protocols,
        ICardioTrackRepository tracks,
        ICurrentUserService currentUser,
        PlanCalendarService calendar,
        ILocalClock clock,
        ISender sender)
    {
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
        _hevyLinks = hevyLinks ?? throw new ArgumentNullException(nameof(hevyLinks));
        _protocols = protocols ?? throw new ArgumentNullException(nameof(protocols));
        _tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public async Task<Result<IReadOnlyList<UpcomingDayDto>>> Handle(GetUpcomingDaysQuery request, CancellationToken cancellationToken)
    {
        if (request.Days is < 1 or > MaxDays)
        {
            return Result.Failure<IReadOnlyList<UpcomingDayDto>>($"Days must be 1–{MaxDays}.", ErrorCode.ValidationFailed);
        }

        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<IReadOnlyList<UpcomingDayDto>>(user);
        }

        var load = await PlanAccess.LoadActiveAsync(_plans, user.Value, cancellationToken);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<IReadOnlyList<UpcomingDayDto>>(load);
        }

        var plan = load.Value;
        var from = request.From ?? _clock.Today();
        var history = await PlanAccess.LiftingHistoryAsync(_workouts, plan, cancellationToken);
        var routines = await HevyRoutineLookup.LoadAsync(_hevyLinks, user.Value, cancellationToken);

        var days = Enumerable.Range(0, request.Days)
            .Select(offset => _calendar.DayFor(plan, from.AddDays(offset), history))
            .ToList();

        var lifts = await LiftPlansAsync(plan, days, user.Value, cancellationToken);

        return Result.Success<IReadOnlyList<UpcomingDayDto>>(days.Select(day =>
        {
            var dto = day.ToDto(plan.Id, routines);
            var dayLifts = day.Sessions
                .Where(s => s.Session.Kind == SessionKind.Lift && s.Status is SessionStatus.Planned or SessionStatus.Missed && s.Session.LiftDay is not null)
                .Select(s => lifts.GetValueOrDefault((s.Session.LiftDay!.Value, s.Session.Label)))
                .OfType<UpcomingLiftDto>()
                .ToList();
            return new UpcomingDayDto(dto.Date, dto.Phase, dto.Sessions, dayLifts);
        }).ToList());
    }

    /// <summary>One planned lift per (A2S2 day, slot label) in the window, built by the authoritative week-plan query.</summary>
    private async Task<Dictionary<(DayNumber Day, string Label), UpcomingLiftDto>> LiftPlansAsync(
        TrainingPlan plan, IReadOnlyList<PlanDay> days, UserId userId, CancellationToken ct)
    {
        var result = new Dictionary<(DayNumber, string), UpcomingLiftDto>();
        var slots = days.SelectMany(d => d.Sessions)
            .Where(s => s.Session.Kind == SessionKind.Lift && s.Session.LiftDay is not null)
            .Select(s => s.Session)
            .DistinctBy(s => (s.LiftDay, s.Label))
            .ToList();
        if (slots.Count == 0 || plan.LinkedWorkoutId is not { } workoutId)
        {
            return result;
        }

        var workout = await _workouts.GetByIdAsync(workoutId, ct);
        if (workout is null || workout.Status == WorkoutStatus.Completed)
        {
            return result;
        }

        var needsFinishers = slots.Any(s => s.Finisher is not null);
        var protocols = needsFinishers ? await _protocols.ListForUserAsync(userId, ct) : [];
        var tracks = needsFinishers ? await _tracks.ListForUserAsync(userId, ct) : [];

        foreach (var slot in slots)
        {
            var week = await _sender.Send(new GetWeekPlanQuery(workoutId.Value, workout.CurrentWeek, (int)slot.LiftDay!.Value), ct);
            if (week.IsFailure)
            {
                continue;
            }

            UpcomingFinisherDto? finisher = null;
            if (slot.Finisher?.Fixed is { } cooldown)
            {
                finisher = new UpcomingFinisherDto(cooldown.DisplayName(), cooldown.Describe());
            }
            else if (slot.Finisher?.CardioTrackId is { } trackId && tracks.FirstOrDefault(t => t.Id == trackId) is { } track)
            {
                finisher = new UpcomingFinisherDto(track.Name, track.Prescription.Title);
            }
            else if (slot.Finisher?.CardioProtocolId is { } protocolId && protocols.FirstOrDefault(p => p.Id == protocolId) is { } protocol)
            {
                finisher = new UpcomingFinisherDto(protocol.Name, string.IsNullOrWhiteSpace(protocol.Notes) ? $"{protocol.TotalMinutes} min" : $"{protocol.TotalMinutes} min · {protocol.Notes}");
            }

            result[(slot.LiftDay.Value, slot.Label)] = new UpcomingLiftDto(
                slot.LiftDay.Value,
                slot.Label,
                week.Value.WeekNumber,
                week.Value.IsDeloadWeek,
                week.Value.Exercises
                    .Where(e => e.PlannedSets.Count > 0 || e.CardioBlock is not null)
                    .OrderBy(e => e.OrderInDay)
                    .Select(e => new UpcomingExerciseDto(e.Name, e.Category, e.PlannedSets.Select(s => new UpcomingSetDto(s.WeightKg, s.TargetReps, s.IsAmrap)).ToList(),
                        e.Metadata.IsTrainingMaxPending, e.Metadata.IntensityPercentage, e.CardioBlock?.Text))
                    .ToList(),
                finisher)
            {
                Cardio = PlannedCardioBuilder.For(slot.Finisher, tracks, protocols, week.Value.Exercises.Count + 1)
            };
        }

        return result;
    }
}
