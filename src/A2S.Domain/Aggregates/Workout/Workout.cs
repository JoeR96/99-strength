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
    public UserId UserId { get; private set; }
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
        UserId userId,
        string name,
        ProgramVariant variant,
        List<int> blockSequence,
        IEnumerable<Exercise> exercises)
        : base(id)
    {
        CheckRule(userId.Value != Guid.Empty, "User ID cannot be empty");
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
        UserId userId,
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
        var performancesList = performances.ToList();
        var exercisesForDay = ValidateDayCompletion(day, performancesList);

        // Capture snapshots BEFORE applying progression
        var snapshots = CapturePreProgressionSnapshots(exercisesForDay);

        ApplyPerformancesToExercises(day, performancesList);

        RecordSkippedProgressionAuditEntries(day, performancesList, exercisesForDay);

        RecordActivity(day, performancesList, snapshots);

        ProgressAfterDayCompletion();
    }

    private List<Exercise> ValidateDayCompletion(DayNumber day, List<ExercisePerformance> performancesList)
    {
        CheckRule(Status == WorkoutStatus.Active,
            "Cannot complete a day when workout is not active");
        CheckRule(!IsDayCompletedInCurrentWeek(day),
            $"{day} has already been completed in Week {CurrentWeek}");
        CheckRule(performancesList.Any(),
            "At least one exercise performance is required");

        var exercisesForDay = _exercises.Where(e => e.AssignedDay == day).ToList();
        CheckRule(exercisesForDay.Any(),
            $"No exercises are assigned to {day}");

        return exercisesForDay;
    }

    private List<ProgressionSnapshot> CapturePreProgressionSnapshots(List<Exercise> exercisesForDay)
    {
        return exercisesForDay
            .Select(e => e.CaptureProgressionSnapshot())
            .ToList();
    }

    private void ApplyPerformancesToExercises(DayNumber day, List<ExercisePerformance> performancesList)
    {
        foreach (var performance in performancesList)
        {
            var exercise = _exercises.FirstOrDefault(e => e.Id == performance.ExerciseId);
            CheckRule(exercise != null,
                $"Exercise {performance.ExerciseId} not found in this workout");
            CheckRule(exercise.AssignedDay == day,
                $"Exercise {exercise.Name} is not assigned to {day}");

            if (!performance.SkipProgression)
            {
                var domainEvent = exercise.ApplyProgression(performance);
                if (domainEvent != null)
                {
                    AddDomainEvent(domainEvent);
                }
            }
        }
    }

    private void RecordSkippedProgressionAuditEntries(
        DayNumber day,
        List<ExercisePerformance> performancesList,
        List<Exercise> exercisesForDay)
    {
        foreach (var perf in performancesList.Where(p => p.SkipProgression))
        {
            var exercise = exercisesForDay.FirstOrDefault(e => e.Id == perf.ExerciseId);
            if (exercise != null)
            {
                _auditEntries.Add(ProgressionAuditEntry.TemporarySubstitution(
                    perf.ExerciseId,
                    exercise.Name,
                    CurrentWeek,
                    (int)day,
                    "Temporary substitution - progression skipped"));
                AddDomainEvent(new ProgressionSkipped(Id, perf.ExerciseId.Value, exercise.Name, CurrentWeek, "Temporary substitution"));
            }
        }
    }

    private void RecordActivity(
        DayNumber day,
        List<ExercisePerformance> performancesList,
        List<ProgressionSnapshot> snapshots)
    {
        var activity = new WorkoutActivity(day, CurrentWeek, CurrentBlock, performancesList, snapshots);
        _completedActivities.Add(activity);

        AddDomainEvent(new DayCompleted(Id, day, CurrentWeek, performancesList.Count));
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

        AdvanceToNextWeek();
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
    /// Confirms the starting weight for an exercise via the aggregate root.
    /// </summary>
    public void ConfirmExerciseStartingWeight(ExerciseId exerciseId, Weight weight)
    {
        var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId)
            ?? throw new InvalidOperationException($"Exercise {exerciseId} not found in this workout");
        exercise.ConfirmStartingWeight(weight);
    }

    /// <summary>
    /// Confirms the new working weight for a Cable/Machine exercise after progression.
    /// Clears the pending weight confirmation flag and applies the user-confirmed weight.
    /// </summary>
    public void ConfirmExerciseWorkingWeight(ExerciseId exerciseId, Weight confirmedWeight)
    {
        var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId)
            ?? throw new InvalidOperationException($"Exercise {exerciseId} not found in this workout");
        exercise.ConfirmWorkingWeight(confirmedWeight);
    }

    /// <summary>
    /// Updates the working weight for a non-linear progression exercise.
    /// </summary>
    public void UpdateExerciseWorkingWeight(ExerciseId exerciseId, Weight weight)
    {
        var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId)
            ?? throw new InvalidOperationException($"Exercise {exerciseId} not found in this workout");
        exercise.UpdateWeight(weight);
    }

    /// <summary>
    /// Gets an exercise by its ID. Returns the internal reference for read operations.
    /// All mutations must go through Workout aggregate methods.
    /// </summary>
    public Exercise? GetExerciseById(ExerciseId exerciseId)
    {
        return _exercises.FirstOrDefault(e => e.Id == exerciseId);
    }

    /// <summary>
    /// Substitutes an exercise with a different exercise.
    /// Preserves all progression data, only changes the name and optionally the External template ID.
    /// </summary>
    /// <param name="exerciseId">The exercise to substitute</param>
    /// <param name="newExerciseName">The new exercise name</param>
    /// <param name="newExternalTemplateId">Optional new External template ID</param>
    /// <returns>The original exercise name for audit purposes</returns>
    public string SubstituteExercise(ExerciseId exerciseId, string newExerciseName, string? newExternalTemplateId = null)
    {
        var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId);
        CheckRule(exercise != null, $"Exercise {exerciseId} not found in this workout");

        return exercise.Substitute(newExerciseName, newExternalTemplateId);
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
            if (CurrentWeek < TotalWeeks)
            {
                AdvanceToNextWeek();
            }
            else
            {
                CompleteProgram();
            }
        }
        else
        {
            var nextDay = GetNextDayToComplete();
            if (nextDay.HasValue)
            {
                CurrentDay = (int)nextDay.Value;
            }
        }
    }

    private void AdvanceToNextWeek()
    {
        var previousWeek = CurrentWeek;
        CurrentWeek++;
        CurrentDay = 1;
        CurrentBlock = CalculateBlockNumber(CurrentWeek);

        var isDeloadWeek = IsDeloadWeek();
        AddDomainEvent(new WeekProgressed(Id, previousWeek, CurrentWeek, CurrentBlock, isDeloadWeek));
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
    /// Records an audit entry for tracking progression changes.
    /// </summary>
    public void RecordAuditEntry(ProgressionAuditEntry entry)
    {
        _auditEntries.Add(entry);
    }

    /// <summary>
    /// Replaces a completed activity at the given index with a corrected version.
    /// Used by retrofix operations to correct historical snapshot data.
    /// </summary>
    public void ReplaceCompletedActivity(int index, WorkoutActivity replacement)
    {
        CheckRule(index >= 0 && index < _completedActivities.Count,
            $"Activity index {index} out of range");
        _completedActivities[index] = replacement;
    }

    /// <summary>
    /// Sets the training max for an exercise using polymorphic dispatch.
    /// Supported only by linear progression; other strategies throw InvalidOperationException.
    /// Used by retrofix operations to correct historical TM values.
    /// </summary>
    public void SetExerciseTrainingMax(ExerciseId exerciseId, TrainingMax trainingMax)
    {
        var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId)
            ?? throw new InvalidOperationException($"Exercise {exerciseId} not found in this workout");

        exercise.Progression.UpdateTrainingMaxValue(trainingMax);
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
                var exercise = _exercises.FirstOrDefault(e => e.Id == snapshot.ExerciseId);
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
