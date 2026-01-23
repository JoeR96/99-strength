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
    public WorkoutStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public IReadOnlyCollection<Exercise> Exercises => _exercises.AsReadOnly();
    public IReadOnlyCollection<WorkoutActivity> CompletedActivities => _completedActivities.AsReadOnly();

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
        int totalWeeks,
        IEnumerable<Exercise> exercises)
        : base(id)
    {
        CheckRule(!string.IsNullOrWhiteSpace(userId), "User ID cannot be empty");
        CheckRule(!string.IsNullOrWhiteSpace(name), "Workout name cannot be empty");
        CheckRule(totalWeeks > 0, "Total weeks must be greater than zero");

        var exercisesList = exercises.ToList();
        CheckRule(exercisesList.Any(), "Workout must have at least one exercise");

        // Validate exercise ordering
        ValidateExerciseOrdering(exercisesList);

        UserId = userId;
        Name = name;
        Variant = variant;
        TotalWeeks = totalWeeks;
        CurrentWeek = 1;
        CurrentBlock = 1;
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
    /// <param name="totalWeeks">The total number of weeks in the program (default: 21).</param>
    public static Workout Create(
        string userId,
        string name,
        ProgramVariant variant,
        IEnumerable<Exercise> exercises,
        int totalWeeks = 21)
    {
        return new Workout(
            new WorkoutId(Guid.NewGuid()),
            userId,
            name,
            variant,
            totalWeeks,
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
    /// </summary>
    public void CompleteDay(DayNumber day, IEnumerable<ExercisePerformance> performances)
    {
        CheckRule(Status == WorkoutStatus.Active,
            "Cannot complete a day when workout is not active");

        var performancesList = performances.ToList();
        CheckRule(performancesList.Any(),
            "At least one exercise performance is required");

        // Validate that all performances are for exercises in this workout on this day
        var exercisesForDay = _exercises.Where(e => e.AssignedDay == day).ToList();
        CheckRule(exercisesForDay.Any(),
            $"No exercises are assigned to {day}");

        foreach (var performance in performancesList)
        {
            var exercise = _exercises.FirstOrDefault(e => e.Id == performance.ExerciseId);
            CheckRule(exercise != null,
                $"Exercise {performance.ExerciseId} not found in this workout");
            CheckRule(exercise.AssignedDay == day,
                $"Exercise {exercise.Name} is not assigned to {day}");

            // Apply progression to the exercise
            exercise.ApplyProgression(performance);
        }

        // Record the completed activity
        var activity = new WorkoutActivity(day, CurrentWeek, CurrentBlock, performancesList);
        _completedActivities.Add(activity);

        AddDomainEvent(new DayCompleted(Id, day, CurrentWeek, performancesList.Count));
    }

    /// <summary>
    /// Progresses to the next week.
    /// Updates week number and block number accordingly.
    /// </summary>
    public void ProgressToNextWeek()
    {
        CheckRule(Status == WorkoutStatus.Active,
            "Cannot progress week when workout is not active");
        CheckRule(CurrentWeek < TotalWeeks,
            $"Cannot progress beyond week {TotalWeeks}");

        var previousWeek = CurrentWeek;
        CurrentWeek++;

        // Update block number (Block 1: weeks 1-7, Block 2: weeks 8-14, Block 3: weeks 15-21)
        CurrentBlock = CalculateBlockNumber(CurrentWeek);

        var isDeloadWeek = IsDeloadWeek();

        AddDomainEvent(new WeekProgressed(Id, previousWeek, CurrentWeek, CurrentBlock, isDeloadWeek));

        // Check if program is complete
        if (CurrentWeek == TotalWeeks && isDeloadWeek)
        {
            CompleteProgram();
        }
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
    /// Gets planned sets for all exercises on a specific day.
    /// </summary>
    public IEnumerable<PlannedSet> GetPlannedSetsForDay(DayNumber day)
    {
        var exercisesForDay = _exercises
            .Where(e => e.AssignedDay == day)
            .OrderBy(e => e.OrderInDay);

        var allPlannedSets = new List<PlannedSet>();
        foreach (var exercise in exercisesForDay)
        {
            var sets = exercise.CalculatePlannedSets(CurrentWeek, CurrentBlock);
            allPlannedSets.AddRange(sets);
        }

        return allPlannedSets;
    }

    /// <summary>
    /// Gets planned sets for a specific exercise.
    /// </summary>
    public IEnumerable<PlannedSet> GetPlannedSetsForExercise(ExerciseId exerciseId)
    {
        var exercise = _exercises.FirstOrDefault(e => e.Id == exerciseId);
        CheckRule(exercise != null, $"Exercise {exerciseId} not found in this workout");

        return exercise.CalculatePlannedSets(CurrentWeek, CurrentBlock);
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
    /// Calculates the block number based on week number.
    /// Block 1: weeks 1-7, Block 2: weeks 8-14, Block 3: weeks 15-21.
    /// </summary>
    private static int CalculateBlockNumber(int weekNumber)
    {
        return weekNumber switch
        {
            <= 7 => 1,
            <= 14 => 2,
            <= 21 => 3,
            _ => throw new ArgumentException($"Week number {weekNumber} exceeds standard 21-week program")
        };
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
