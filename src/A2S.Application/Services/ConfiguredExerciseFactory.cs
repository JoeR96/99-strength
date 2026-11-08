using A2S.Application.Commands.CreateWorkout;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;

namespace A2S.Application.Services;

/// <summary>
/// Turns <see cref="CreateExerciseRequest"/>s into <see cref="Exercise"/>s against the exercise library.
/// Shared by <c>CreateWorkout</c> and the suggested plan seed so both resolve templates the same way.
/// </summary>
public static class ConfiguredExerciseFactory
{
    /// <summary>
    /// Builds every exercise in request order. Throws <see cref="InvalidOperationException"/> when a
    /// template name is not in the library: silently skipping would leave a gap in a day's
    /// OrderInDay sequence, which the Workout aggregate then rejects with a confusing error.
    /// </summary>
    public static List<Exercise> Create(IExerciseLibraryProvider library, IReadOnlyList<CreateExerciseRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(requests);

        var exercises = new List<Exercise>(requests.Count);

        foreach (var request in requests)
        {
            var template = library.GetByName(request.TemplateName)
                ?? throw new InvalidOperationException(
                    $"Exercise template '{request.TemplateName}' was not found in the exercise library. " +
                    "Add it to the library (exercise-library.json) before using it in a workout.");

            exercises.Add(request.ProgressionType switch
            {
                "Linear" => Linear(template, request),
                "MinimalSets" => MinimalSets(template, request),
                _ => RepsPerSet(template, request)
            });
        }

        return exercises;
    }

    private static Exercise Linear(ExerciseTemplate template, CreateExerciseRequest request) =>
        Exercise.CreateWithLinearProgression(
            name: template.Name,
            category: request.Category,
            equipment: template.Equipment,
            assignedDay: request.AssignedDay,
            orderInDay: request.OrderInDay,
            externalTemplateId: request.ExternalTemplateId,
            trainingMax: TrainingMax.Create(request.TrainingMaxValue ?? 100m, request.TrainingMaxUnit ?? WeightUnit.Kilograms),
            // Linear progression uses AMRAP on the last set unless the request says otherwise.
            useAmrap: request.UseAmrap ?? true,
            baseSetsPerExercise: template.DefaultSets ?? 4);

    private static Exercise MinimalSets(ExerciseTemplate template, CreateExerciseRequest request) =>
        Exercise.CreateWithMinimalSetsProgression(
            name: template.Name,
            category: request.Category,
            equipment: template.Equipment,
            assignedDay: request.AssignedDay,
            orderInDay: request.OrderInDay,
            externalTemplateId: request.ExternalTemplateId,
            startingWeight: Weight.Create(request.StartingWeight ?? 30m, request.WeightUnit ?? WeightUnit.Kilograms),
            targetTotalReps: request.TargetTotalReps ?? 40,
            startingSets: request.StartingSets ?? 3);

    private static Exercise RepsPerSet(ExerciseTemplate template, CreateExerciseRequest request)
    {
        var repRange = request.RepRangeMinimum.HasValue && request.RepRangeMaximum.HasValue
            ? RepRange.Create(request.RepRangeMinimum.Value, request.RepRangeMaximum.Value)
            : template.DefaultRepRange ?? RepRange.Common.Medium;

        // Weight is optional: without one it is confirmed after the first session.
        Weight? startingWeight = request.StartingWeight.HasValue && request.WeightUnit.HasValue
            ? Weight.Create(request.StartingWeight.Value, request.WeightUnit.Value)
            : null;

        return Exercise.CreateWithRepsPerSetProgression(
            name: template.Name,
            category: request.Category,
            equipment: template.Equipment,
            assignedDay: request.AssignedDay,
            orderInDay: request.OrderInDay,
            externalTemplateId: request.ExternalTemplateId,
            repRange: repRange,
            startingSets: request.StartingSets ?? template.DefaultSets ?? 3,
            targetSets: request.TargetSets ?? (template.DefaultSets ?? 3) + 2,
            isUnilateral: request.IsUnilateral,
            startingWeight: startingWeight);
    }
}
