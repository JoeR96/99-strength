using A2S.Application.Cardio;
using A2S.Application.Interfaces.Hevy;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;

namespace A2S.Application.Hevy;

/// <summary>
/// Brief § 4e pull: when a matched Hevy workout contains a built-in cardio exercise (Stair Machine,
/// Walking, Treadmill, Running), creates the cardio log from its sets and evaluates the person's one
/// track of that modality. Idempotent per Hevy workout id + exercise index, so a re-pulled or edited
/// workout never evaluates twice. Changes are saved by the caller's unit of work.
/// </summary>
public sealed class HevyCardioTrackLogger
{
    private readonly ICardioTrackRepository _tracks;
    private readonly CardioTrackSessionRecorder _recorder;
    private readonly ILocalClock _clock;

    public HevyCardioTrackLogger(ICardioTrackRepository tracks, CardioTrackSessionRecorder recorder, ILocalClock clock)
    {
        _tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public static string Key(string workoutId, int exerciseIndex) => $"hevy:{workoutId}:{exerciseIndex}";

    /// <param name="skipTrack">A track the session completion already evaluated (a cardio slot pointing at it).</param>
    /// <returns>How many track sessions were newly recorded.</returns>
    public async Task<int> LogAsync(UserId userId, HevyWorkout workout, DateOnly date, CardioTrackId? skipTrack, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(workout);
        var cardio = workout.Exercises.Where(e => HevyTemplateCatalogue.IsBuiltInCardio(e.ExerciseTemplateId)).ToList();
        if (cardio.Count == 0)
        {
            return 0;
        }

        var tracks = (await _tracks.ListForUserAsync(userId, ct))
            .Where(t => t.Status != CardioTrackStatus.Completed && t.Id != skipTrack)
            .ToList();

        var recorded = 0;
        foreach (var exercise in cardio)
        {
            var modalities = HevyTemplateCatalogue.ModalitiesForTemplate(exercise.ExerciseTemplateId);
            var candidates = tracks.Where(t => modalities.Contains(t.Modality)).ToList();
            // Ambiguous (two walking tracks, or a treadmill with both a walk and a run track): prefer the one active track, else skip.
            var active = candidates.Where(t => t.Status == CardioTrackStatus.Active).ToList();
            var track = active.Count == 1 ? active[0] : candidates.Count == 1 ? candidates[0] : null;
            var log = LogFrom(exercise);
            if (track is null || log is null)
            {
                continue;
            }

            var result = await _recorder.RecordAsync(userId, track.Id, Key(workout.Id, exercise.Index), date, log,
                completedAsPrescribed: true, _clock.NowUtc(), ct);
            if (result is { Result.AlreadyRecorded: false })
            {
                recorded++;
            }
        }

        return recorded;
    }

    /// <summary>Duration, distance, floors (stair floors) or steps, and the hardest RPE from one exercise's sets.</summary>
    public static CardioLog? LogFrom(HevyWorkoutExercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        var sets = exercise.Sets.ToList();
        var seconds = sets.Sum(s => s.DurationSeconds ?? 0);
        if (seconds <= 0)
        {
            return null;
        }

        var distance = sets.Sum(s => s.DistanceMeters ?? 0);
        var metric = sets.Sum(s => s.CustomMetric ?? 0m);
        var rpe = sets.Where(s => s.Rpe.HasValue).Select(s => (int)Math.Round(s.Rpe!.Value)).DefaultIfEmpty(0).Max();
        var isFloors = exercise.ExerciseTemplateId == HevyTemplateCatalogue.StairMachineFloors;
        var isSteps = exercise.ExerciseTemplateId == HevyTemplateCatalogue.StairMachineSteps;

        return new CardioLog(
            seconds,
            null,
            rpe is >= 1 and <= 10 ? rpe : null,
            distance > 0 ? distance : null,
            null,
            floors: isFloors && metric > 0 ? (int)metric : null,
            steps: isSteps && metric > 0 ? (int)metric : null,
            source: CardioLogSource.Hevy);
    }
}
