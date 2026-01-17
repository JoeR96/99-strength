using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using MediatR;

namespace A2S.Application.Commands.CreateWorkout;

/// <summary>
/// Handler for CreateWorkoutCommand.
/// Creates a new workout program with default exercises (the 4 main lifts).
/// </summary>
public sealed class CreateWorkoutCommandHandler : IRequestHandler<CreateWorkoutCommand, Result<Guid>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateWorkoutCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<Guid>> Handle(CreateWorkoutCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if there's already an active workout
            var existingWorkout = await _workoutRepository.GetActiveWorkoutAsync(cancellationToken);
            if (existingWorkout != null)
            {
                return Result.Failure<Guid>("An active workout already exists. Complete or pause it before creating a new one.");
            }

            // Create default exercises for the A2S program
            var exercises = CreateDefaultExercises();

            // Create the workout
            var workout = Workout.Create(
                request.Name,
                request.Variant,
                exercises,
                request.TotalWeeks);

            // Start the workout immediately
            workout.Start();

            // Persist
            await _workoutRepository.AddAsync(workout, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(workout.Id.Value);
        }
        catch (Exception ex)
        {
            return Result.Failure<Guid>($"Failed to create workout: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates the default set of exercises for the A2S program.
    /// This includes the 4 main lifts: Squat, Bench Press, Deadlift, and Overhead Press.
    /// Each is assigned to a different training day with default training maxes.
    /// </summary>
    private static List<Exercise> CreateDefaultExercises()
    {
        // Default training maxes (can be customized later via API)
        var defaultTrainingMax = TrainingMax.Create(100m, WeightUnit.Kilograms);

        var exercises = new List<Exercise>
        {
            // Day 1: Squat + Bench Press
            Exercise.CreateWithLinearProgression(
                name: "Squat",
                category: ExerciseCategory.MainLift,
                equipment: EquipmentType.Barbell,
                assignedDay: DayNumber.Day1,
                orderInDay: 1,
                trainingMax: defaultTrainingMax,
                useAmrap: true,
                baseSetsPerExercise: 4),

            Exercise.CreateWithLinearProgression(
                name: "Bench Press",
                category: ExerciseCategory.MainLift,
                equipment: EquipmentType.Barbell,
                assignedDay: DayNumber.Day1,
                orderInDay: 2,
                trainingMax: defaultTrainingMax,
                useAmrap: true,
                baseSetsPerExercise: 4),

            // Day 2: Deadlift + Overhead Press
            Exercise.CreateWithLinearProgression(
                name: "Deadlift",
                category: ExerciseCategory.MainLift,
                equipment: EquipmentType.Barbell,
                assignedDay: DayNumber.Day2,
                orderInDay: 1,
                trainingMax: defaultTrainingMax,
                useAmrap: true,
                baseSetsPerExercise: 4),

            Exercise.CreateWithLinearProgression(
                name: "Overhead Press",
                category: ExerciseCategory.MainLift,
                equipment: EquipmentType.Barbell,
                assignedDay: DayNumber.Day2,
                orderInDay: 2,
                trainingMax: defaultTrainingMax,
                useAmrap: true,
                baseSetsPerExercise: 4)
        };

        return exercises;
    }
}
