using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
using A2S.Domain.ValueObjects;

namespace A2S.Domain.Aggregates.Workout;

/// <summary>
/// Workout aggregate root representing a complete training program.
/// Manages exercises, progression, and weekly program flow.
/// </summary>
/// <remarks>
/// Reference: research/business-rules.md Section 1 "Program Structure Rules"
/// - Standard program is 21 weeks, divided into 3 blocks of 7 weeks each
/// - Week 7, 14, and 21 are deload weeks
/// - Block intensity increases: Block 1 &lt; Block 2 &lt; Block 3
/// - Block reps decrease: Block 1 &gt; Block 2 &gt; Block 3
/// </remarks>
public sealed class Workout : AggregateRoot<WorkoutId>
{
    private readonly List<Exercise> _exercises = new();
    private readonly List<WorkoutActivity> _completedActivities = new();
    private readonly List<WorkoutActivity> _archivedActivities = new();
    private readonly List<ProgressionAuditEntry> _auditEntries = new();
    private List<int> _blockSequence = new() { 1, 2, 3 };

    /// <summary>
    /// The ID of the user who owns this workout.
    /// Used to scope workouts to individual users.
    /// </summary>
    public string UserId { get; private set; } = string.Empty;
    public string Name { get; private set; }
    public ProgramVariant Variant { get; private set; }
    public int TotalWeeks { get; private set; }
    public int CurrentWeek { get; private set; }
    public int CurrentBlock { get; private set; }

    /// <summary>
    /// The current day number (1-6) the user is on within the current week.
    /// Resets to 1 when progressing to a new week.
    /// </summary>
    public int CurrentDay { get; private set; }

    public WorkoutStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// The Hevy routine folder ID used to organize routines for this program.
    /// </summary>
    public string? HevyRoutineFolderId { get; private set; }

    /// <summary>
    /// Tracks synced Hevy routine IDs by week and day.
    /// Format: { "week1-day1": "routine-id-123", "week2-day1": "routine-id-456" }
    /// Used for lifecycle management (delete old routines when week completes).
    /// </summary>
    public Dictionary<string, string> HevySyncedRoutines { get; private set; } = new();

    /// <summary>
    /// The block sequence defining which block types to run and in what order.
    /// Default is [1, 2, 3] for the standard 21-week program.
    /// Example: [1, 1, 2, 3] runs Block 1 twice, then Block 2, then Block 3 (28 weeks).
    /// </summary>
    public IReadOnlyList<int> BlockSequence => _blockSequence.AsReadOnly();

    public IReadOnlyCollection<Exercise> Exercises => _exercises.AsReadOnly();
    public IReadOnlyCollection<WorkoutActivity> CompletedActivities => _completedActivities.AsReadOnly();
    public IReadOnlyCollection<WorkoutActivity> ArchivedActivities => _archivedActivities.AsReadOnly();
    public IReadOnlyCollection<ProgressionAuditEntry> AuditEntries => _auditEntries.AsReadOnly();

    // EF Core constructor
    private Workout()
    {
        Name = string.Empty;
    }

    private Workout(
        WorkoutId id,
        string userId,
        string name,
        ProgramVariant variant,
        List<int> blockSequence,
        IEnumerable<Exercise> exercises)
        : base(id)
    {
        CheckRule(!string.IsNullOrWhiteSpace(userId), "User ID cannot be empty");
        CheckRule(!string.IsNullOrWhiteSpace(name), "Workout name cannot be empty");
        ValidateBlockSequence(blockSequence);

        var exercisesList = exercises.ToList();
        CheckRule(exercisesList.Any(), "Workout must have at least one exercise");

        // Validate exercise ordering
        ValidateExerciseOrdering(exercisesList);

        UserId = userId;
        Name = name;
        Variant = variant;
        _blockSequence = new List<int>(blockSequence);
        TotalWeeks = blockSequence.Count * 7;
        CurrentWeek = 1;
        CurrentBlock = blockSequence[0];
        CurrentDay = 1;
        Status = WorkoutStatus.NotStarted;
        CreatedAt = DateTime.UtcNow;
        _exercises.AddRange(exercisesList);

        AddDomainEvent(new WorkoutCreated(id, name, variant, exercisesList.Count));
    }

    /// <summary>
    /// Creates a new workout program with the specified exercises.
    /// Standard A2S program is 21 weeks (3 blocks of 7 weeks).
    /// </summary>
    /// <param name="userId">The ID of the user who owns this workout.</param>
    /// <param name="name">The name of the workout program.</param>
    /// <param name="variant">The program variant (e.g., FiveDay).</param>
    /// <param name="exercises">The exercises included in the program.</param>
    /// <param name="blockSequence">The block sequence (e.g., [1,2,3] or [1,1,2,3]). Defaults to [1,2,3].</param>
    public static Workout Create(
        string userId,
        string name,
        ProgramVariant variant,
        IEnumerable<Exercise> exercises,
        List<int>? blockSequence = null)
    {
        var sequence = blockSequence ?? new List<int> { 1, 2, 3 };
        return new Workout(
            new WorkoutId(Guid.NewGuid()),
            userId,
            name,
            variant,
            sequence,
            exercises);
    }

    /// <summary>
    /// Starts the workout program.
    /// Can only be called when status is NotStarted.
    /// </summary>
    public void Start()
    {
        CheckRule(Status == WorkoutStatus.NotStarted,
            "Workout can only be started when status is NotStarted");

        Status = WorkoutStatus.Active;
        StartedAt = DateTime.UtcNow;

        AddDomainEvent(new WorkoutStarted(Id));
    }

    /// <summary>
    /// Completes a training day with exercise performances.
    /// Applies progression logic to all exercises based on their performance.
    /// Automatically progresses to next day, or next week if all days in week are complete.
    /// </summary>
    public void CompleteDay(DayNumber day, IEnumerable<ExercisePerformance> performances)
    {
        CheckRule(Status == WorkoutStatus.Active,
            "Cannot complete a day when workout is not active");

        // Validate that this day hasn't already been completed this week
        CheckRule(!IsDayCompletedInCurrentWeek(day),
            $"{day} has already been completed in Week {CurrentWeek}");

        var performancesList = performances.ToList();
        CheckRule(performancesList.Any(),
            "At least one exercise performance is required");

        // Validate that all performances are for exercises in this workout on this day
        var exercisesForDay = _exercises.Where(e => e.AssignedDay == day).ToList();
        CheckRule(exercisesForDay.Any(),
            $"No exercises are assigned to {day}");

        // Capture snapshots BEFORE applying progression
        var snapshots = exercisesForDay
            .Select(e => e.CaptureProgressionSnapshot())
            .ToList();

        foreach (var performance in performancesList)
        {
            var exercise = _exercises.FirstOrDefault(e => e.Id == performance.ExerciseId);
            CheckRule(exercise != null,
                $"Exercise {performance.ExerciseId} not found in this workout");
            CheckRule(exercise.AssignedDay == day,
                $"Exercise {exercise.Name} is not assigned to {day}");

            // Apply progression to the exercise (unless it was a temporary substitution)
            if (!performance.SkipProgression)
            {
                exercise.ApplyProgression(performance);
            }
        }

        // Record audit entries for exercises that skipped progression
        foreach (var perf in performancesList.Where(p => p.SkipProgression))
        {
            var exercise = exercisesForDay.FirstOrDefault(e => e.Id == perf.ExerciseId);
            if (exercise != null)
            {
                _auditEntries.Add(ProgressionAuditEntry.TemporarySubstitution(
                    perf.ExerciseId.Value,
                    exercise.Name,
                    CurrentWeek,
                    (int)day,
                    "Temporary substitution - progression skipped"));
                AddDomainEvent(new ProgressionSkipped(Id, perf.ExerciseId.Value, exercise.Name, CurrentWeek, "Temporary substitution"));
            }
        }

        // Record the completed activity with progression snapshots
        var activity = new WorkoutActivity(day, CurrentWeek, CurrentBlock, performancesList, snapshots);
        _completedActivities.Add(activity);

        AddDomainEvent(new DayCompleted(Id, day, CurrentWeek, performancesList.Count));

        // Auto-progress to next day or next week
        ProgressAfterDayCompletion();
    }

    /// <summary>
    /// Progresses to the next week.
    /// Updates week number and block number accordingly.
    /// Resets CurrentDay to 1.
    /// </summary>
    public void ProgressToNextWeek()
    {
        CheckRule(Status == WorkoutStatus.Active,
            "Cannot progress week when workout is not active");
        CheckRule(CurrentWeek < TotalWeeks,
            $"Cannot progress beyond week {TotalWeeks}");

        // Validate all days in current week are complete before progressing
        CheckRule(AreAllDaysCompletedInCurrentWeek(),
            $"Cannot progress to next week until all {GetDaysPerWeek()} days are completed in Week {CurrentWeek}");

        var previousWeek = CurrentWeek;
        CurrentWeek++;
        CurrentDay = 1; // Reset to day 1 for the new week

        // Update block number (Block 1: weeks 1-7, Block 2: weeks 8-14, Block 3: weeks 15-21)
        CurrentBlock = CalculateBlockNumber(CurrentWeek);

        var isDeloadWeek = IsDeloadWeek();

        AddDomainEvent(new WeekProgressed(Id, previousWeek, CurrentWeek, CurrentBlock, isDeloadWeek));

        // Check if program is complete (at start of final deload week)
        // Note: program completes after completing the final week's workouts
    }

    /// <summary>
    /// Pauses the workout program.
    /// Can be resumed later.
    /// </summary>
    public void Pause()
    {
        CheckRule(Status == WorkoutStatus.Active,
            "Can only pause an active workout");

        Status = WorkoutStatus.Paused;
    }

    /// <summary>
    /// Resumes a paused workout program.
    /// </summary>
    public void Resume()
    {
        CheckRule(Status == WorkoutStatus.Paused,
            "Can only resume a paused workout");

        Status = WorkoutStatus.Active;
    }

    /// <summary>
    /// Sets this workout as the active program.
    /// Can only be called on workouts that are not completed.
    /// If the workout was NotStarted, it will be started.
    /// If it was Paused, it will be resumed.
    /// </summary>
    public void SetAsActive()
    {
        CheckRule(Status != WorkoutStatus.Completed,
            "Cannot activate a completed workout");

        if (Status == WorkoutStatus.NotStarted)
        {
            Start();
        }
        else if (Status == WorkoutStatus.Paused)
        {
            Resume();
        }
        // If already Active, no-op
    }

    /// <summary>
    /// Deactivates this workout by pausing it.
    /// Only active workouts can be deactivated.
    /// </summary>
    public void Deactivate()
    {
        if (Status == WorkoutStatus.Active)
        {
            Pause();
        }
        // If not active, no-op (allows idempotent deactivation)
    }

    /// <summary>
    /// Manually adjusts the Training Max for an exercise.
    /// Only applicable for exercises using linear progression.
    /// </summary>
    public void AdjustTrainingMax(ExerciseId exerciseId, TrainingMax newTm, string? reason = null)
    {
        var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId);
        CheckRule(exercise != null, $"Exercise {exerciseId} not found in this workout");

        exercise.UpdateTrainingMax(newTm, reason);
    }

    /// <summary>
    /// Manually adjusts the starting weight for an accessory exercise.
    /// Only applicable for exercises using reps-per-set progression.
    /// </summary>
    public void AdjustStartingWeight(ExerciseId exerciseId, Weight newWeight)
    {
        var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId);
        CheckRule(exercise != null, $"Exercise {exerciseId} not found in this workout");

        exercise.UpdateStartingWeight(newWeight);
    }

    /// <summary>
    /// Manually adjusts the weight for exercises using weight-based progression.
    /// Works for both RepsPerSet and MinimalSets exercises.
    /// </summary>
    public void AdjustWeight(ExerciseId exerciseId, Weight newWeight)
    {
        var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId);
        CheckRule(exercise != null, $"Exercise {exerciseId} not found in this workout");

        exercise.UpdateWeight(newWeight);
    }

    /// <summary>
    /// Sets whether an exercise is unilateral (performed one side at a time).
    /// Only applicable for exercises using RepsPerSet progression.
    /// </summary>
    public void SetExerciseUnilateral(ExerciseId exerciseId, bool isUnilateral)
    {
        var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId);
        CheckRule(exercise != null, $"Exercise {exerciseId} not found in this workout");

        exercise.SetUnilateral(isUnilateral);
    }

    /// <summary>
    /// Gets an exercise by its ID.
    /// </summary>
    public Exercise? GetExerciseById(ExerciseId exerciseId)
    {
        return _exercises.FirstOrDefault(e => e.Id == exerciseId);
    }

    /// <summary>
    /// Substitutes an exercise with a different exercise.
    /// Preserves all progression data, only changes the name and optionally the Hevy template ID.
    /// </summary>
    /// <param name="exerciseId">The exercise to substitute</param>
    /// <param name="newExerciseName">The new exercise name</param>
    /// <param name="newHevyExerciseTemplateId">Optional new Hevy template ID</param>
    /// <returns>The original exercise name for audit purposes</returns>
    public string SubstituteExercise(ExerciseId exerciseId, string newExerciseName, string? newHevyExerciseTemplateId = null)
    {
        var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId);
        CheckRule(exercise != null, $"Exercise {exerciseId} not found in this workout");

        return exercise.Substitute(newExerciseName, newHevyExerciseTemplateId);
    }

    /// <summary>
    /// Gets planned sets for all exercises on a specific day.
    /// Translates the current program week to a template week for the WeeklyProgram table.
    /// </summary>
    public IEnumerable<PlannedSet> GetPlannedSetsForDay(DayNumber day)
    {
        var exercisesForDay = _exercises
            .Where(e => e.AssignedDay == day)
            .OrderBy(e => e.OrderInDay);

        var templateWeek = GetTemplateWeek(CurrentWeek);
        var blockType = GetBlockType(CurrentWeek);

        var allPlannedSets = new List<PlannedSet>();
        foreach (var exercise in exercisesForDay)
        {
            var sets = exercise.CalculatePlannedSets(templateWeek, blockType);
            allPlannedSets.AddRange(sets);
        }

        return allPlannedSets;
    }

    /// <summary>
    /// Gets planned sets for a specific exercise.
    /// Translates the current program week to a template week for the WeeklyProgram table.
    /// </summary>
    public IEnumerable<PlannedSet> GetPlannedSetsForExercise(ExerciseId exerciseId)
    {
        var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId);
        CheckRule(exercise != null, $"Exercise {exerciseId} not found in this workout");

        var templateWeek = GetTemplateWeek(CurrentWeek);
        var blockType = GetBlockType(CurrentWeek);
        return exercise.CalculatePlannedSets(templateWeek, blockType);
    }

    /// <summary>
    /// Gets all exercises assigned to a specific day.
    /// </summary>
    public IEnumerable<Exercise> GetExercisesForDay(DayNumber day)
    {
        return _exercises
            .Where(e => e.AssignedDay == day)
            .OrderBy(e => e.OrderInDay);
    }

    /// <summary>
    /// Checks if the current week is a deload week.
    /// Deload weeks occur every 7th week (weeks 7, 14, 21).
    /// </summary>
    public bool IsDeloadWeek()
    {
        return CurrentWeek % 7 == 0;
    }

    /// <summary>
    /// Gets the current block number (1, 2, or 3).
    /// </summary>
    public int GetCurrentBlockNumber()
    {
        return CurrentBlock;
    }

    /// <summary>
    /// Translates a program week number (1-N) to a template week (1-21)
    /// that indexes into the standard WeeklyProgram table.
    /// </summary>
    public int GetTemplateWeek(int programWeek)
    {
        CheckRule(programWeek >= 1 && programWeek <= TotalWeeks,
            $"Program week {programWeek} must be between 1 and {TotalWeeks}");

        var blockIndex = (programWeek - 1) / 7;
        var blockType = _blockSequence[blockIndex];
        var weekInBlock = ((programWeek - 1) % 7) + 1;
        return ((blockType - 1) * 7) + weekInBlock;
    }

    /// <summary>
    /// Gets the block type (1, 2, or 3) for a given program week
    /// based on the block sequence.
    /// </summary>
    public int GetBlockType(int programWeek)
    {
        CheckRule(programWeek >= 1 && programWeek <= TotalWeeks,
            $"Program week {programWeek} must be between 1 and {TotalWeeks}");

        var blockIndex = (programWeek - 1) / 7;
        return _blockSequence[blockIndex];
    }

    /// <summary>
    /// Updates the block sequence for this workout.
    /// Recalculates TotalWeeks and CurrentBlock accordingly.
    /// Cannot shorten the program past the current week position.
    /// </summary>
    public void UpdateBlockSequence(List<int> newSequence)
    {
        ValidateBlockSequence(newSequence);

        var newTotalWeeks = newSequence.Count * 7;

        // Allow restart on completed workouts: reset week and status
        if (Status == WorkoutStatus.Completed)
        {
            _blockSequence = new List<int>(newSequence);
            TotalWeeks = newTotalWeeks;
            CurrentWeek = 1;
            CurrentDay = 1;
            CurrentBlock = CalculateBlockNumber(1);
            Status = WorkoutStatus.Active;
            CompletedAt = null;
            _archivedActivities.AddRange(_completedActivities);
            _completedActivities.Clear();
            AddDomainEvent(new ProgramRestarted(Id, _archivedActivities.Count));
            return;
        }

        CheckRule(CurrentWeek <= newTotalWeeks,
            $"Cannot shorten program to {newTotalWeeks} weeks when already at week {CurrentWeek}");

        _blockSequence = new List<int>(newSequence);
        TotalWeeks = newTotalWeeks;
        CurrentBlock = CalculateBlockNumber(CurrentWeek);
    }

    /// <summary>
    /// Adds a new exercise to the workout.
    /// </summary>
    public void AddExercise(Exercise exercise)
    {
        CheckRule(Status == WorkoutStatus.NotStarted || Status == WorkoutStatus.Active,
            "Cannot add exercises to a completed or paused workout");

        // Validate no duplicate exercise on same day with same order
        var conflictingExercise = _exercises.FirstOrDefault(e =>
            e.AssignedDay == exercise.AssignedDay &&
            e.OrderInDay == exercise.OrderInDay);

        CheckRule(conflictingExercise == null,
            $"An exercise already exists at position {exercise.OrderInDay} on {exercise.AssignedDay}");

        _exercises.Add(exercise);
    }

    /// <summary>
    /// Removes an exercise from the workout.
    /// </summary>
    public void RemoveExercise(ExerciseId exerciseId)
    {
        CheckRule(Status == WorkoutStatus.NotStarted || Status == WorkoutStatus.Active,
            "Cannot remove exercises from a completed or paused workout");

        var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId);
        CheckRule(exercise != null, $"Exercise {exerciseId} not found in this workout");

        _exercises.Remove(exercise);
    }

    /// <summary>
    /// Reorders an exercise within its assigned day.
    /// </summary>
    public void ReorderExercise(ExerciseId exerciseId, int newOrderInDay)
    {
        var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId);
        CheckRule(exercise != null, $"Exercise {exerciseId} not found in this workout");

        exercise.ChangeAssignedDay(exercise.AssignedDay, newOrderInDay);
    }

    /// <summary>
    /// Gets the number of training days per week based on the program variant.
    /// </summary>
    public int GetDaysPerWeek()
    {
        return Variant switch
        {
            ProgramVariant.FourDay => 4,
            ProgramVariant.FiveDay => 5,
            ProgramVariant.SixDay => 6,
            _ => 4 // Default to 4 days
        };
    }

    /// <summary>
    /// Checks if a specific day has been completed in the current week.
    /// </summary>
    public bool IsDayCompletedInCurrentWeek(DayNumber day)
    {
        return _completedActivities.Any(a =>
            a.WeekNumber == CurrentWeek && a.Day == day);
    }

    /// <summary>
    /// Gets the list of days completed in the current week.
    /// </summary>
    public IReadOnlyList<DayNumber> GetCompletedDaysInCurrentWeek()
    {
        return _completedActivities
            .Where(a => a.WeekNumber == CurrentWeek)
            .Select(a => a.Day)
            .Distinct()
            .OrderBy(d => (int)d)
            .ToList();
    }

    /// <summary>
    /// Checks if all training days for the current week have been completed.
    /// </summary>
    public bool AreAllDaysCompletedInCurrentWeek()
    {
        var completedDays = GetCompletedDaysInCurrentWeek();
        return completedDays.Count >= GetDaysPerWeek();
    }

    /// <summary>
    /// Gets the next day to complete in the current week.
    /// Returns null if all days are complete.
    /// </summary>
    public DayNumber? GetNextDayToComplete()
    {
        var completedDays = GetCompletedDaysInCurrentWeek();
        var allDays = GetAllTrainingDaysForVariant();

        return allDays.FirstOrDefault(d => !completedDays.Contains(d));
    }

    /// <summary>
    /// Gets all training days for this program variant.
    /// </summary>
    private IEnumerable<DayNumber> GetAllTrainingDaysForVariant()
    {
        var daysPerWeek = GetDaysPerWeek();
        return Enum.GetValues<DayNumber>()
            .Where(d => (int)d <= daysPerWeek)
            .OrderBy(d => (int)d);
    }

    /// <summary>
    /// Automatically progresses to the next day or week after completing a day.
    /// </summary>
    private void ProgressAfterDayCompletion()
    {
        if (AreAllDaysCompletedInCurrentWeek())
        {
            // All days complete - progress to next week if not at the end
            if (CurrentWeek < TotalWeeks)
            {
                var previousWeek = CurrentWeek;
                CurrentWeek++;
                CurrentDay = 1;
                CurrentBlock = CalculateBlockNumber(CurrentWeek);

                var isDeloadWeek = IsDeloadWeek();
                AddDomainEvent(new WeekProgressed(Id, previousWeek, CurrentWeek, CurrentBlock, isDeloadWeek));
            }
            else
            {
                // Final week completed - program is done
                CompleteProgram();
            }
        }
        else
        {
            // Progress to next day in the current week
            var nextDay = GetNextDayToComplete();
            if (nextDay.HasValue)
            {
                CurrentDay = (int)nextDay.Value;
            }
        }
    }

    /// <summary>
    /// Calculates the block type based on week number and block sequence.
    /// </summary>
    private int CalculateBlockNumber(int weekNumber)
    {
        if (_blockSequence == null || _blockSequence.Count == 0)
        {
            // Legacy fallback
            return weekNumber switch
            {
                <= 7 => 1,
                <= 14 => 2,
                <= 21 => 3,
                _ => 3
            };
        }

        var blockIndex = (weekNumber - 1) / 7;
        if (blockIndex >= _blockSequence.Count)
            throw new ArgumentException($"Week number {weekNumber} exceeds program length of {_blockSequence.Count * 7} weeks");

        return _blockSequence[blockIndex];
    }

    /// <summary>
    /// Marks the program as completed.
    /// </summary>
    private void CompleteProgram()
    {
        Status = WorkoutStatus.Completed;
        CompletedAt = DateTime.UtcNow;

        AddDomainEvent(new WorkoutCompleted(Id, CompletedAt.Value));
    }

    /// <summary>
    /// Sets the Hevy routine folder ID for this workout.
    /// </summary>
    public void SetHevyRoutineFolderId(string folderId)
    {
        CheckRule(!string.IsNullOrWhiteSpace(folderId), "Hevy routine folder ID cannot be empty");
        HevyRoutineFolderId = folderId;
    }

    /// <summary>
    /// Records a synced Hevy routine ID for a specific week and day.
    /// </summary>
    public void SetHevySyncedRoutine(int weekNumber, int dayNumber, string routineId)
    {
        CheckRule(weekNumber > 0 && weekNumber <= TotalWeeks, "Week number must be valid");
        CheckRule(dayNumber > 0 && dayNumber <= GetDaysPerWeek(), "Day number must be valid");
        CheckRule(!string.IsNullOrWhiteSpace(routineId), "Routine ID cannot be empty");

        var key = $"week{weekNumber}-day{dayNumber}";
        HevySyncedRoutines[key] = routineId;
    }

    /// <summary>
    /// Gets the synced Hevy routine ID for a specific week and day.
    /// Returns null if no routine is synced for that week/day.
    /// </summary>
    public string? GetHevySyncedRoutine(int weekNumber, int dayNumber)
    {
        var key = $"week{weekNumber}-day{dayNumber}";
        return HevySyncedRoutines.TryGetValue(key, out var routineId) ? routineId : null;
    }

    /// <summary>
    /// Removes a synced Hevy routine ID for a specific week and day.
    /// </summary>
    public void RemoveHevySyncedRoutine(int weekNumber, int dayNumber)
    {
        var key = $"week{weekNumber}-day{dayNumber}";
        HevySyncedRoutines.Remove(key);
    }

    /// <summary>
    /// Records an audit entry for tracking progression changes.
    /// </summary>
    public void RecordAuditEntry(ProgressionAuditEntry entry)
    {
        _auditEntries.Add(entry);
    }

    /// <summary>
    /// Retrofixes the Training Max history for a Linear progression exercise.
    /// Recalculates TM values from the original starting TM using unrounded math,
    /// then updates all snapshot JSON and the current exercise TM.
    /// Used to fix data that was incorrectly rounded to gym increments.
    /// </summary>
    /// <returns>A summary of old vs new TM values per week.</returns>
    public List<(int Week, decimal OldTm, decimal NewTm)> RetrofixLinearTmHistory(
        ExerciseId exerciseId, decimal originalStartingTm)
    {
        var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId)
            ?? throw new InvalidOperationException($"Exercise {exerciseId} not found in this workout");
        if (exercise.Progression is not LinearProgressionStrategy linear)
            throw new InvalidOperationException($"Exercise {exercise.Name} does not use Linear progression");
        var changes = new List<(int Week, decimal OldTm, decimal NewTm)>();

        // Sort activities chronologically for this exercise's day
        var activitiesForExercise = _completedActivities
            .Where(a => a.Performances.Any(p => p.ExerciseId == exerciseId))
            .OrderBy(a => a.WeekNumber)
            .ThenBy(a => a.CompletedAt)
            .ToList();

        if (!activitiesForExercise.Any())
            return changes;

        // Walk through activities, recalculating TM with proper precision
        var currentTm = originalStartingTm;

        foreach (var activity in activitiesForExercise)
        {
            // Find the snapshot for this exercise (captured BEFORE progression)
            var snapshotIndex = activity.ProgressionSnapshots
                .FindIndex(s => s.ExerciseId == exerciseId.Value);

            if (snapshotIndex >= 0)
            {
                var oldSnapshot = activity.ProgressionSnapshots[snapshotIndex];
                var oldJson = System.Text.Json.JsonDocument.Parse(oldSnapshot.ProgressionStateJson);
                var oldTmValue = oldJson.RootElement.GetProperty("TrainingMaxValue").GetDecimal();

                // Create corrected snapshot with the recalculated TM
                var correctedJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    TrainingMaxValue = currentTm,
                    TrainingMaxUnit = oldJson.RootElement.GetProperty("TrainingMaxUnit").GetInt32(),
                    UseAmrap = oldJson.RootElement.GetProperty("UseAmrap").GetBoolean(),
                    BaseSetsPerExercise = oldJson.RootElement.GetProperty("BaseSetsPerExercise").GetInt32()
                });

                // Replace the snapshot in the list
                activity.ProgressionSnapshots[snapshotIndex] = new ProgressionSnapshot(
                    oldSnapshot.ExerciseId,
                    oldSnapshot.ExerciseName,
                    oldSnapshot.ProgressionType,
                    correctedJson);

                changes.Add((activity.WeekNumber, oldTmValue, currentTm));
            }

            // Now apply the AMRAP delta to get the TM for the NEXT week
            var performance = activity.Performances.FirstOrDefault(p => p.ExerciseId == exerciseId);
            if (performance != null && !performance.SkipProgression)
            {
                var delta = performance.GetAmrapDelta();
                var adjustment = AmrapDeltaTable.GetAdjustment(delta);

                if (adjustment.Type != ValueObjects.AdjustmentType.None)
                {
                    currentTm = Math.Round(currentTm * (1 + adjustment.Amount), 2);
                }
            }
        }

        // Update the current exercise TM to the correctly calculated value
        linear.RestoreState(TrainingMax.Create(currentTm, linear.TrainingMax.Unit));

        return changes;
    }

    /// <summary>
    /// Undoes the last completed day, restoring progression state.
    /// Only allows undoing the most recent completion (single undo).
    /// </summary>
    public UndoResult UndoLastCompletion()
    {
        CheckRule(Status == WorkoutStatus.Active, "Cannot undo when workout is not active");
        CheckRule(_completedActivities.Any(), "No completed activities to undo");

        // Get the last activity (most recent)
        var lastActivity = _completedActivities
            .OrderByDescending(a => a.CompletedAt)
            .First();

        // Check if this would cross week boundary
        var wouldRollbackWeek = lastActivity.WeekNumber < CurrentWeek;

        // Restore progression snapshots (if available - older activities may not have them)
        var snapshots = lastActivity.ProgressionSnapshots;
        if (snapshots != null && snapshots.Count > 0)
        {
            foreach (var snapshot in snapshots)
            {
                var exercise = _exercises.FirstOrDefault(e => e.Id.Value == snapshot.ExerciseId);
                exercise?.RestoreFromSnapshot(snapshot);
            }
        }
        // Note: If no snapshots exist (older completion), we can only remove the activity
        // but cannot restore the progression state. The user should be aware of this.

        // Remove the activity
        _completedActivities.Remove(lastActivity);

        // Adjust current day/week
        if (wouldRollbackWeek)
        {
            CurrentWeek = lastActivity.WeekNumber;
            CurrentBlock = CalculateBlockNumber(CurrentWeek);
        }
        CurrentDay = (int)lastActivity.Day;

        // Record audit entry
        var hasSnapshots = snapshots != null && snapshots.Count > 0;
        _auditEntries.Add(ProgressionAuditEntry.UndoCompletion(
            lastActivity.WeekNumber,
            (int)lastActivity.Day,
            hasSnapshots ? "User initiated undo" : "User initiated undo (progression not restored - completed before snapshot feature)"));

        AddDomainEvent(new CompletionUndone(Id, lastActivity.Day, lastActivity.WeekNumber));

        return new UndoResult(lastActivity.Day, lastActivity.WeekNumber, wouldRollbackWeek);
    }

    /// <summary>
    /// Validates a block sequence: non-empty, each element 1-3, max 10 blocks.
    /// </summary>
    private static void ValidateBlockSequence(List<int> sequence)
    {
        CheckRule(sequence != null && sequence.Count > 0, "Block sequence cannot be empty");
        CheckRule(sequence!.Count <= 10, "Block sequence cannot exceed 10 blocks (70 weeks)");
        CheckRule(sequence.All(b => b >= 1 && b <= 3),
            "Each block type must be 1, 2, or 3");
    }

    /// <summary>
    /// Validates that exercises have proper ordering (no gaps, no duplicates).
    /// </summary>
    private static void ValidateExerciseOrdering(List<Exercise> exercises)
    {
        var exercisesByDay = exercises.GroupBy(e => e.AssignedDay);

        foreach (var dayGroup in exercisesByDay)
        {
            var orders = dayGroup.Select(e => e.OrderInDay).OrderBy(o => o).ToList();

            // Check for no duplicates
            var duplicates = orders.GroupBy(o => o).Where(g => g.Count() > 1).ToList();
            if (duplicates.Any())
            {
                throw new InvalidOperationException(
                    $"Duplicate order numbers found for {dayGroup.Key}: {string.Join(", ", duplicates.Select(d => d.Key))}");
            }

            // Check ordering starts at 1 and is sequential
            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i] != i + 1)
                {
                    throw new InvalidOperationException(
                        $"Exercise ordering for {dayGroup.Key} must be sequential starting from 1. Found gap at position {i + 1}");
                }
            }
        }
    }
}

/// <summary>
/// Result of an undo operation, indicating what was undone.
/// </summary>
public sealed record UndoResult(
    DayNumber Day,
    int WeekNumber,
    bool WeekRolledBack);
