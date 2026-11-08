using A2S.Application.Services;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;

namespace A2S.Application.Tests.SuggestedPlans;

/// <summary>
/// The slice of the production exercise library the suggested plan uses, with the muscle groups the
/// boot-time seeder and Hevy backfill give each row (exercise-library.json + hevy-exercises.json).
/// Keeps the Application tests honest about the volume budget without a database.
/// </summary>
public sealed class SeedLibrary : IExerciseLibraryProvider
{
    private static readonly (string Name, EquipmentType Equipment, string Primary, string[] Secondary)[] Rows =
    [
        ("Bench Press (Barbell)", EquipmentType.Barbell, "chest", ["triceps", "shoulders"]),
        ("Chest Supported Incline Row (Dumbbell)", EquipmentType.Dumbbell, "upper_back", ["lats", "biceps", "forearms"]),
        ("Single Arm Landmine Press (Barbell)", EquipmentType.Barbell, "shoulders", ["triceps", "chest"]),
        ("Single Arm Cable Row", EquipmentType.Machine, "upper_back", ["lats", "biceps", "forearms"]),
        ("Face Pull", EquipmentType.Machine, "shoulders", ["biceps"]),
        ("Squat (Barbell)", EquipmentType.Barbell, "quadriceps", ["hamstrings", "glutes"]),
        ("Romanian Deadlift (Barbell)", EquipmentType.Barbell, "hamstrings", ["glutes", "lower_back", "upper_back", "lats"]),
        ("Bulgarian Split Squat", EquipmentType.Dumbbell, "quadriceps", ["glutes", "hamstrings"]),
        ("Side Plank", EquipmentType.Bodyweight, "abdominals", []),
        // The library spells it "Pallof" and Hevy "Palloff", so the backfill finds no secondaries for it.
        ("Cable Core Pallof Press", EquipmentType.Machine, "abdominals", []),
        ("Overhead Press (Barbell)", EquipmentType.Barbell, "shoulders", ["triceps", "abdominals"]),
        ("Lat Pulldown (Cable)", EquipmentType.Machine, "lats", ["upper_back", "biceps", "forearms"]),
        ("Incline Bench Press (Dumbbell)", EquipmentType.Dumbbell, "chest", ["triceps", "shoulders"]),
        ("Seal Row (Barbell)", EquipmentType.Barbell, "upper_back", []),
        ("Wall Slide with Lift-Off", EquipmentType.Bodyweight, "shoulders", []),
        ("Deadlift (Barbell)", EquipmentType.Barbell, "glutes", ["hamstrings", "quadriceps", "lower_back", "upper_back", "lats", "traps"]),
        ("Leg Press (Machine)", EquipmentType.Machine, "quadriceps", ["glutes", "hamstrings"]),
        ("Single Leg Romanian Deadlift (Dumbbell)", EquipmentType.Dumbbell, "hamstrings", ["glutes", "lower_back"]),
        ("Copenhagen Plank (Short Lever)", EquipmentType.Bodyweight, "adductors", []),
        ("Dead Bug", EquipmentType.Bodyweight, "abdominals", [])
    ];

    public IReadOnlyList<ExerciseTemplate> AllTemplates { get; } = Rows
        .Select(r => new ExerciseTemplate(r.Name, r.Equipment, RepRange.Common.Medium, 3, $"Test: {r.Primary}", r.Primary, r.Secondary))
        .ToList();

    public ExerciseTemplate? GetByName(string name) =>
        AllTemplates.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public Task<IReadOnlyList<ExerciseTemplate>> GetAllTemplatesAsync(CancellationToken ct = default) =>
        Task.FromResult(AllTemplates);

    /// <summary>The lookup shape <c>VolumeBudgetService.HardSetsPerGroup</c> takes.</summary>
    public (string Primary, IReadOnlyList<string> Secondary)? MuscleGroups(string key)
    {
        var template = GetByName(key);
        return template is null ? null : (template.MuscleGroup, template.SecondaryMuscleGroups);
    }
}
