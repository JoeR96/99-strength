using System.Text.Json;
using A2S.Application.Interfaces.Hevy;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Enums;

namespace A2S.Application.Hevy;

/// <summary>One exercise of an inbox workout, enough to recognise it.</summary>
public sealed record HevyInboxExerciseDto(string Title, string TemplateId, int Sets);

/// <summary>A pulled Hevy workout the plan could not place, as the inbox shows it.</summary>
public sealed record HevyInboxItemDto(
    string HevyWorkoutId,
    string Title,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    DateOnly LocalDate,
    string? RoutineId,
    string? Reason,
    IReadOnlyList<HevyInboxExerciseDto> Exercises)
{
    public static HevyInboxItemDto From(HevyWorkoutSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var exercises = new List<HevyInboxExerciseDto>();
        try
        {
            var workout = JsonSerializer.Deserialize<HevyWorkout>(snapshot.RawJson);
            if (workout is not null)
            {
                exercises.AddRange(workout.Exercises.Select(e => new HevyInboxExerciseDto(e.Title ?? e.ExerciseTemplateId, e.ExerciseTemplateId, e.Sets.Count)));
            }
        }
        catch (JsonException)
        {
            // An unreadable snapshot still shows in the inbox, just without its exercises.
        }

        return new HevyInboxItemDto(
            snapshot.Id.Value,
            snapshot.Title,
            snapshot.StartUtc,
            snapshot.EndUtc,
            snapshot.LocalDate,
            snapshot.RoutineId,
            snapshot.Reason,
            exercises);
    }
}

/// <summary>What assigning an inbox workout produced.</summary>
public sealed record AssignHevyWorkoutResult(string HevyWorkoutId, Guid CompletionId, DateOnly Date, SessionKind Kind, DayNumber? LiftDay);
