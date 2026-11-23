using A2S.Application.Common;
using A2S.Application.Hevy;
using A2S.Application.Interfaces.Hevy;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using MediatR;

namespace A2S.Application.Queries.Suggestions;

/// <summary>
/// Suggested starting numbers for a workout's pending exercises (A2S2 lifts without a training max,
/// reps-per-set exercises without a starting weight), estimated from the person's Hevy history.
/// </summary>
public sealed record GetStartingNumbersSuggestionsQuery(Guid WorkoutId) : IQuery<Result<StartingNumbersSuggestionsDto>>;

/// <summary><c>HevyStatus</c>: <c>ok</c>, <c>noKey</c> or <c>unavailable</c>; the UI falls back to manual input.</summary>
public sealed record StartingNumbersSuggestionsDto(
    string HevyStatus,
    string? HevyMessage,
    IReadOnlyList<StartingNumberItemDto> Items);

/// <summary>One pending exercise. <c>Kind</c> is <c>TrainingMax</c> or <c>StartingWeight</c>; <c>MatchedBy</c> is <c>templateId</c>, <c>title</c>, <c>catalogueName</c> or null.</summary>
public sealed record StartingNumberItemDto(
    Guid ExerciseId,
    string ExerciseName,
    string Kind,
    string? MatchedBy,
    StartingNumberSuggestionDto? Suggestion);

public sealed record StartingNumberSuggestionDto(
    decimal ValueKg,
    decimal EstimatedOneRepMaxKg,
    DateOnly SetDate,
    decimal SetWeightKg,
    int SetReps,
    DateOnly WindowStart,
    DateOnly WindowEnd,
    bool Stale,
    int StalePenaltyPercent,
    string Reason);

public sealed class GetStartingNumbersSuggestionsQueryHandler
    : IRequestHandler<GetStartingNumbersSuggestionsQuery, Result<StartingNumbersSuggestionsDto>>
{
    private readonly IWorkoutRepository _workouts;
    private readonly ICurrentUserService _currentUser;
    private readonly HevyStrengthHistory _history;
    private readonly HevyTemplateCatalogue _catalogue;
    private readonly ILocalClock _clock;

    public GetStartingNumbersSuggestionsQueryHandler(
        IWorkoutRepository workouts,
        ICurrentUserService currentUser,
        HevyStrengthHistory history,
        HevyTemplateCatalogue catalogue,
        ILocalClock clock)
    {
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<StartingNumbersSuggestionsDto>> Handle(
        GetStartingNumbersSuggestionsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        if (userId is null)
        {
            return Result.Failure<StartingNumbersSuggestionsDto>("User must be authenticated.", ErrorCode.Unauthenticated);
        }

        var workout = await _workouts.GetByIdAsync(new WorkoutId(request.WorkoutId), cancellationToken);
        if (workout is null || workout.UserId != userId.Value)
        {
            return Result.Failure<StartingNumbersSuggestionsDto>("Workout not found.", ErrorCode.NotFound);
        }

        var pending = workout.Exercises
            .Where(e => e.IsTrainingMaxPending || e.Progression is RepsPerSetStrategy { IsWeightPending: true })
            .OrderBy(e => e.AssignedDay)
            .ThenBy(e => e.OrderInDay)
            .ToList();
        if (pending.Count == 0)
        {
            return Result.Success(new StartingNumbersSuggestionsDto("ok", null, []));
        }

        var history = await _history.LoadAsync(userId.Value, cancellationToken);
        var templateLinks = await _catalogue.TemplateIdsAsync(userId.Value, cancellationToken);
        var today = _clock.Today();

        var items = pending.Select(exercise =>
        {
            var (matchedBy, sets) = SetsFor(exercise, history.Workouts, templateLinks);
            var suggestion = exercise.Progression is RepsPerSetStrategy rps
                ? TrainingMaxSuggester.ForStartingWeight(sets, today, rps.Equipment, rps.RepRange)
                : TrainingMaxSuggester.ForTrainingMax(sets, today);
            return new StartingNumberItemDto(
                exercise.Id.Value,
                exercise.Name,
                exercise.Progression is RepsPerSetStrategy ? nameof(StartingNumberKind.StartingWeight) : nameof(StartingNumberKind.TrainingMax),
                sets.Count == 0 ? null : matchedBy,
                suggestion is null ? null : ToDto(suggestion));
        }).ToList();

        var status = history.Status switch
        {
            HevyHistoryStatus.NoKey => "noKey",
            HevyHistoryStatus.Unavailable => "unavailable",
            _ => "ok"
        };
        return Result.Success(new StartingNumbersSuggestionsDto(status, history.Message, items));
    }

    /// <summary>The exercise's Hevy sets: by template id first, then exact title, then the catalogue name.</summary>
    private (string? MatchedBy, List<DatedSet> Sets) SetsFor(
        Exercise exercise, IReadOnlyList<HevyWorkout> workouts, IReadOnlyDictionary<string, string> templateLinks)
    {
        var templateId = WorkoutMatcher.HevyTemplateIdFor(exercise, templateLinks);
        if (!string.IsNullOrWhiteSpace(templateId))
        {
            var byId = Collect(workouts, e => string.Equals(e.ExerciseTemplateId, templateId, StringComparison.Ordinal));
            if (byId.Count > 0)
            {
                return ("templateId", byId);
            }
        }

        var byTitle = Collect(workouts, e => string.Equals(e.Title?.Trim(), exercise.Name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (byTitle.Count > 0)
        {
            return ("title", byTitle);
        }

        if (HevyTemplateCatalogue.IsCustom(exercise.ExternalTemplateId))
        {
            var catalogueName = exercise.ExternalTemplateId[(exercise.ExternalTemplateId.IndexOf(':') + 1)..].Trim();
            var byCatalogue = Collect(workouts, e => string.Equals(e.Title?.Trim(), catalogueName, StringComparison.OrdinalIgnoreCase));
            if (byCatalogue.Count > 0)
            {
                return ("catalogueName", byCatalogue);
            }
        }

        return (null, []);
    }

    private List<DatedSet> Collect(IReadOnlyList<HevyWorkout> workouts, Func<HevyWorkoutExercise, bool> matches) =>
        workouts
            .SelectMany(w => w.Exercises.Where(matches).SelectMany(e => e.Sets.Select(s => (w.StartTime, Set: s))))
            .Where(x => x.Set.WeightKg is > 0 && x.Set.Reps is > 0)
            .Select(x => new DatedSet(
                _clock.ToLocalDate(x.StartTime),
                x.Set.WeightKg!.Value,
                x.Set.Reps!.Value,
                IsWarmup: !HevySetTypes.IsWorking(x.Set.Type)))
            .ToList();

    private static StartingNumberSuggestionDto ToDto(StartingNumberSuggestion s) => new(
        s.ValueKg,
        s.Evidence.EstimatedOneRepMaxKg,
        s.Evidence.Set.Date,
        s.Evidence.Set.WeightKg,
        s.Evidence.Set.Reps,
        s.Evidence.WindowStart,
        s.Evidence.WindowEnd,
        s.Stale,
        s.StalePenaltyPercent,
        s.Reason);
}
