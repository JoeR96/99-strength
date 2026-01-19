using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;

namespace A2S.Application.Services;

/// <summary>
/// Provides a library of predefined exercise templates for the A2S program.
/// Templates contain only exercise metadata - users choose category, progression type,
/// day assignment, and order when adding to their workout.
/// </summary>
public static class ExerciseLibrary
{
    /// <summary>
    /// Exercise template containing only metadata about an exercise.
    /// Users will configure category, progression, day, and order when adding to workout.
    /// </summary>
    public record ExerciseTemplate(
        string Name,
        EquipmentType Equipment,
        RepRange? DefaultRepRange = null,
        int? DefaultSets = null,
        string Description = "");

    /// <summary>
    /// Gets all available exercise templates.
    /// </summary>
    public static IReadOnlyList<ExerciseTemplate> AllTemplates => new[]
    {
        // Main Compound Lifts
        new ExerciseTemplate(
            Name: "Squat",
            Equipment: EquipmentType.Barbell,
            DefaultRepRange: RepRange.Common.MediumLow,
            DefaultSets: 4,
            Description: "Back Squat - primary lower body compound movement"),

        new ExerciseTemplate(
            Name: "Bench Press",
            Equipment: EquipmentType.Barbell,
            DefaultRepRange: RepRange.Common.MediumLow,
            DefaultSets: 4,
            Description: "Barbell Bench Press - primary pushing movement"),

        new ExerciseTemplate(
            Name: "Deadlift",
            Equipment: EquipmentType.Barbell,
            DefaultRepRange: RepRange.Common.MediumLow,
            DefaultSets: 4,
            Description: "Conventional Deadlift - primary pulling movement"),

        new ExerciseTemplate(
            Name: "Overhead Press",
            Equipment: EquipmentType.Barbell,
            DefaultRepRange: RepRange.Common.MediumLow,
            DefaultSets: 4,
            Description: "Standing Barbell Overhead Press - vertical pressing movement"),

        // Compound Variations
        new ExerciseTemplate(
            Name: "Front Squat",
            Equipment: EquipmentType.Barbell,
            DefaultRepRange: RepRange.Common.MediumLow,
            DefaultSets: 4,
            Description: "Front-loaded squat variation"),

        new ExerciseTemplate(
            Name: "Incline Bench Press",
            Equipment: EquipmentType.Barbell,
            DefaultRepRange: RepRange.Common.MediumLow,
            DefaultSets: 4,
            Description: "Incline variation for upper chest emphasis"),

        new ExerciseTemplate(
            Name: "Romanian Deadlift",
            Equipment: EquipmentType.Barbell,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Hip-hinge movement targeting hamstrings"),

        new ExerciseTemplate(
            Name: "Close-Grip Bench Press",
            Equipment: EquipmentType.Barbell,
            DefaultRepRange: RepRange.Common.MediumLow,
            DefaultSets: 4,
            Description: "Narrow grip variation for triceps emphasis"),

        new ExerciseTemplate(
            Name: "Sumo Deadlift",
            Equipment: EquipmentType.Barbell,
            DefaultRepRange: RepRange.Common.MediumLow,
            DefaultSets: 4,
            Description: "Wide-stance deadlift variation"),

        new ExerciseTemplate(
            Name: "Barbell Row",
            Equipment: EquipmentType.Barbell,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Bent-over barbell row for back thickness"),

        // Machine & Dumbbell Compounds
        new ExerciseTemplate(
            Name: "Leg Press",
            Equipment: EquipmentType.Machine,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Machine-based leg exercise"),

        new ExerciseTemplate(
            Name: "Dumbbell Bench Press",
            Equipment: EquipmentType.Dumbbell,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Dumbbell variation for increased range of motion"),

        new ExerciseTemplate(
            Name: "Dumbbell Row",
            Equipment: EquipmentType.Dumbbell,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Single-arm dumbbell row"),

        new ExerciseTemplate(
            Name: "Dumbbell Shoulder Press",
            Equipment: EquipmentType.Dumbbell,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Seated or standing dumbbell press"),

        // Back & Pulling
        new ExerciseTemplate(
            Name: "Lat Pulldown",
            Equipment: EquipmentType.Cable,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Cable lat pulldown for back width"),

        new ExerciseTemplate(
            Name: "Cable Row",
            Equipment: EquipmentType.Cable,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Seated cable row for back thickness"),

        new ExerciseTemplate(
            Name: "Face Pull",
            Equipment: EquipmentType.Cable,
            DefaultRepRange: RepRange.Common.MediumHigh,
            DefaultSets: 3,
            Description: "Cable face pull for rear deltoids"),

        new ExerciseTemplate(
            Name: "Pull-Up",
            Equipment: EquipmentType.Bodyweight,
            DefaultRepRange: RepRange.Common.MediumLow,
            DefaultSets: 3,
            Description: "Bodyweight vertical pull"),

        new ExerciseTemplate(
            Name: "Chin-Up",
            Equipment: EquipmentType.Bodyweight,
            DefaultRepRange: RepRange.Common.MediumLow,
            DefaultSets: 3,
            Description: "Underhand grip vertical pull"),

        // Shoulders
        new ExerciseTemplate(
            Name: "Lateral Raise",
            Equipment: EquipmentType.Dumbbell,
            DefaultRepRange: RepRange.Common.MediumHigh,
            DefaultSets: 3,
            Description: "Dumbbell lateral raises for side delts"),

        new ExerciseTemplate(
            Name: "Rear Delt Fly",
            Equipment: EquipmentType.Dumbbell,
            DefaultRepRange: RepRange.Common.MediumHigh,
            DefaultSets: 3,
            Description: "Dumbbell rear delt flys"),

        new ExerciseTemplate(
            Name: "Front Raise",
            Equipment: EquipmentType.Dumbbell,
            DefaultRepRange: RepRange.Common.MediumHigh,
            DefaultSets: 3,
            Description: "Front delt isolation"),

        // Arms
        new ExerciseTemplate(
            Name: "Bicep Curl",
            Equipment: EquipmentType.Dumbbell,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Dumbbell bicep curls"),

        new ExerciseTemplate(
            Name: "Hammer Curl",
            Equipment: EquipmentType.Dumbbell,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Hammer curls for brachialis and forearms"),

        new ExerciseTemplate(
            Name: "Tricep Extension",
            Equipment: EquipmentType.Cable,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Cable tricep extensions"),

        new ExerciseTemplate(
            Name: "Tricep Pushdown",
            Equipment: EquipmentType.Cable,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Cable tricep pushdowns"),

        new ExerciseTemplate(
            Name: "Dumbbell Tricep Extension",
            Equipment: EquipmentType.Dumbbell,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Overhead dumbbell tricep extension"),

        // Legs
        new ExerciseTemplate(
            Name: "Leg Curl",
            Equipment: EquipmentType.Machine,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Machine leg curl for hamstrings"),

        new ExerciseTemplate(
            Name: "Leg Extension",
            Equipment: EquipmentType.Machine,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Machine leg extension for quadriceps"),

        new ExerciseTemplate(
            Name: "Calf Raise",
            Equipment: EquipmentType.Machine,
            DefaultRepRange: RepRange.Common.MediumHigh,
            DefaultSets: 3,
            Description: "Standing or seated calf raises"),

        new ExerciseTemplate(
            Name: "Hip Thrust",
            Equipment: EquipmentType.Barbell,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Barbell hip thrust for glutes"),

        new ExerciseTemplate(
            Name: "Bulgarian Split Squat",
            Equipment: EquipmentType.Dumbbell,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Single-leg squat variation"),

        new ExerciseTemplate(
            Name: "Lunges",
            Equipment: EquipmentType.Dumbbell,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Walking or stationary lunges"),

        // Core
        new ExerciseTemplate(
            Name: "Ab Wheel",
            Equipment: EquipmentType.Bodyweight,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Ab wheel rollouts"),

        new ExerciseTemplate(
            Name: "Plank",
            Equipment: EquipmentType.Bodyweight,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Front plank hold"),

        new ExerciseTemplate(
            Name: "Cable Crunch",
            Equipment: EquipmentType.Cable,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Cable crunches for abs"),

        new ExerciseTemplate(
            Name: "Hanging Leg Raise",
            Equipment: EquipmentType.Bodyweight,
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Hanging leg raises for lower abs"),
    };

    /// <summary>
    /// Gets an exercise template by name (case-insensitive).
    /// </summary>
    public static ExerciseTemplate? GetByName(string name) =>
        AllTemplates.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
