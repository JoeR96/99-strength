using A2S.Application.Common;
using A2S.Application.Queries.GetWeekPlan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.GetCycle;

/// <summary>
/// The training cycle from where the person is (the Current workout page): the current session (the
/// workout's current week and day, whatever the date), then the rest of this A2S2 week and all of next
/// week. Each entry carries its full planned session from <see cref="GetWeekPlanQuery"/> (sets, finisher
/// item, warm-up), so any of them can be previewed read-only. Null workout id: the active workout.
/// </summary>
public sealed record GetCycleQuery(Guid? WorkoutId) : IQuery<Result<CycleDto>>;

public sealed record CycleDto
{
    public Guid WorkoutId { get; init; }
    public string WorkoutName { get; init; } = string.Empty;
    public int CurrentWeek { get; init; }
    public int CurrentDay { get; init; }
    public int TotalWeeks { get; init; }
    public int DaysPerWeek { get; init; }
    public bool IsCompleted { get; init; }

    /// <summary>The current session first, then the following sessions in cycle order.</summary>
    public IReadOnlyList<CycleSessionDto> Sessions { get; init; } = [];
}

/// <summary>One session of the cycle. <see cref="Position"/> is 1-based within the week ("Session 2 of 4").</summary>
public sealed record CycleSessionDto(int Week, int Day, int Position, bool IsCurrent, bool IsDeloadWeek, string Label, WeekPlanDto Plan);

public sealed class GetCycleQueryHandler : IRequestHandler<GetCycleQuery, Result<CycleDto>>
{
    private readonly IWorkoutRepository _workouts;
    private readonly ICurrentUserService _currentUser;
    private readonly ISender _sender;

    public GetCycleQueryHandler(IWorkoutRepository workouts, ICurrentUserService currentUser, ISender sender)
    {
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public async Task<Result<CycleDto>> Handle(GetCycleQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        if (userId is null)
        {
            return Result.Failure<CycleDto>("User must be authenticated.", ErrorCode.Unauthorized);
        }

        var workout = request.WorkoutId is { } id
            ? await _workouts.GetByIdAsync(new WorkoutId(id), cancellationToken)
            : await _workouts.GetActiveWorkoutAsync(userId.Value, cancellationToken);
        if (workout is null || workout.UserId != userId.Value)
        {
            return Result.Failure<CycleDto>("Workout not found.", ErrorCode.NotFound);
        }

        var days = workout.GetDaysPerWeek();
        var dto = new CycleDto
        {
            WorkoutId = workout.Id.Value,
            WorkoutName = workout.Name,
            CurrentWeek = workout.CurrentWeek,
            CurrentDay = workout.CurrentDay,
            TotalWeeks = workout.TotalWeeks,
            DaysPerWeek = days,
            IsCompleted = workout.Status == WorkoutStatus.Completed
        };
        if (dto.IsCompleted)
        {
            return Result.Success(dto);
        }

        var done = workout.GetCompletedDaysInCurrentWeek().Select(d => (int)d).ToHashSet();
        var positions = Enumerable.Range(1, days)
            .Where(d => !done.Contains(d))
            .OrderBy(d => d == workout.CurrentDay ? 0 : 1).ThenBy(d => d)
            .Select(d => (Week: workout.CurrentWeek, Day: d))
            .ToList();
        if (workout.CurrentWeek < workout.TotalWeeks)
        {
            positions.AddRange(Enumerable.Range(1, days).Select(d => (Week: workout.CurrentWeek + 1, Day: d)));
        }

        var sessions = new List<CycleSessionDto>();
        foreach (var (week, day) in positions)
        {
            var plan = await _sender.Send(new GetWeekPlanQuery(workout.Id.Value, week, day), cancellationToken);
            if (plan.IsFailure)
            {
                continue;
            }

            var isCurrent = week == workout.CurrentWeek && day == workout.CurrentDay;
            sessions.Add(new CycleSessionDto(week, day, day, isCurrent, plan.Value.IsDeloadWeek,
                plan.Value.SessionLabel ?? $"Day {day}", plan.Value with { IsCurrent = isCurrent }));
        }

        return Result.Success(dto with { Sessions = sessions });
    }
}
