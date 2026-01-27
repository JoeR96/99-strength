using A2S.Application.Common;
using A2S.Application.Services;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using MediatR;

namespace A2S.Application.Commands.CreateWorkout;

/// <summary>
/// Handler for CreateWorkoutCommand.
/// Creates a new workout program with a traditional 5-day split including
/// both Linear and RepsPerSet progression exercises.
/// </summary>
public sealed class CreateWorkoutCommandHandler : IRequestHandler<CreateWorkoutCommand, Result<Guid>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public CreateWorkoutCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Result<Guid>> Handle(CreateWorkoutCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Ensure user is authenticated
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Result.Failure<Guid>("User must be authenticated to create a workout.");
            }

            // Check if there's already an active workout for this user
            var existingWorkout = await _workoutRepository.GetActiveWorkoutAsync(userId, cancellationToken);
            if (existingWorkout != null)
            {
                return Result.Failure<Guid>("An active workout already exists. Complete or pause it before creating a new one.");
            }

            // Create exercises - use configured exercises if provided, otherwise use defaults
            var exercises = request.Exercises != null && request.Exercises.Count > 0
                ? CreateConfiguredExercises(request.Exercises)
                : CreateExercisesForVariant(request.Variant);

            // Create the workout
            var workout = Workout.Create(
                userId,
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
    /// Creates exercises from user-configured exercise requests.
    /// </summary>
    private static List<Exercise> CreateConfiguredExercises(IReadOnlyList<CreateExerciseRequest> exerciseRequests)
    {
        var exercises = new List<Exercise>();

        foreach (var exerciseRequest in exerciseRequests)
        {
            // Find the template from the library
            var template = ExerciseLibrary.GetByName(exerciseRequest.TemplateName);
            if (template == null)
            {
                // Skip exercises that don't exist in the library
                continue;
            }

            Exercise exercise;

            if (exerciseRequest.ProgressionType == "Linear")
            {
                // Create Linear progression exercise
                var trainingMax = TrainingMax.Create(
                    exerciseRequest.TrainingMaxValue ?? 100m,
                    exerciseRequest.TrainingMaxUnit ?? WeightUnit.Kilograms
                );

                // All Linear progression exercises use AMRAP on the last set by default
                // This can be overridden via UseAmrap property if provided
                exercise = Exercise.CreateWithLinearProgression(
                    name: template.Name,
                    category: exerciseRequest.Category,
                    equipment: template.Equipment,
                    assignedDay: exerciseRequest.AssignedDay,
                    orderInDay: exerciseRequest.OrderInDay,
                    hevyExerciseTemplateId: exerciseRequest.HevyExerciseTemplateId,
                    trainingMax: trainingMax,
                    useAmrap: exerciseRequest.UseAmrap ?? true, // Default to true for all Linear exercises
                    baseSetsPerExercise: template.DefaultSets ?? 4
                );
            }
            else if (exerciseRequest.ProgressionType == "MinimalSets")
            {
                // Create MinimalSets progression exercise
                var weight = Weight.Create(
                    exerciseRequest.StartingWeight ?? 30m,
                    exerciseRequest.WeightUnit ?? WeightUnit.Kilograms
                );

                exercise = Exercise.CreateWithMinimalSetsProgression(
                    name: template.Name,
                    category: exerciseRequest.Category,
                    equipment: template.Equipment,
                    assignedDay: exerciseRequest.AssignedDay,
                    orderInDay: exerciseRequest.OrderInDay,
                    hevyExerciseTemplateId: exerciseRequest.HevyExerciseTemplateId,
                    startingWeight: weight,
                    targetTotalReps: exerciseRequest.TargetTotalReps ?? 40,
                    startingSets: exerciseRequest.StartingSets ?? 3
                );
            }
            else // RepsPerSet
            {
                // Create RepsPerSet progression exercise
                var weight = Weight.Create(
                    exerciseRequest.StartingWeight ?? 20m,
                    exerciseRequest.WeightUnit ?? WeightUnit.Kilograms
                );

                exercise = Exercise.CreateWithRepsPerSetProgression(
                    name: template.Name,
                    category: exerciseRequest.Category,
                    equipment: template.Equipment,
                    assignedDay: exerciseRequest.AssignedDay,
                    orderInDay: exerciseRequest.OrderInDay,
                    hevyExerciseTemplateId: exerciseRequest.HevyExerciseTemplateId,
                    repRange: template.DefaultRepRange ?? RepRange.Common.Medium,
                    startingWeight: weight,
                    startingSets: exerciseRequest.StartingSets ?? template.DefaultSets ?? 3,
                    targetSets: exerciseRequest.TargetSets ?? (template.DefaultSets ?? 3) + 2,
                    isUnilateral: exerciseRequest.IsUnilateral
                );
            }

            exercises.Add(exercise);
        }

        return exercises;
    }

    /// <summary>
    /// Creates default exercises based on the program variant.
    /// Creates a basic program with the 4 main lifts using Linear progression.
    /// </summary>
    private static List<Exercise> CreateExercisesForVariant(ProgramVariant variant)
    {
        List<Exercise> exercises = new List<Exercise>();

        // Default Hevy exercise template IDs for main lifts
        const string SquatHevyId = "D04AC939";
        const string BenchPressHevyId = "79D0BB3A";
        const string DeadliftHevyId = "C6272009";
        const string OverheadPressHevyId = "7B8D84E8";

        // Create default main 4 lifts with Linear progression
        ExerciseLibrary.ExerciseTemplate? squat = ExerciseLibrary.GetByName("Squat");
        if (squat != null)
        {
            exercises.Add(Exercise.CreateWithLinearProgression(
                name: squat.Name,
                category: ExerciseCategory.MainLift,
                equipment: squat.Equipment,
                assignedDay: DayNumber.Day1,
                orderInDay: 1,
                hevyExerciseTemplateId: SquatHevyId,
                trainingMax: TrainingMax.Create(100m, WeightUnit.Kilograms),
                useAmrap: true,
                baseSetsPerExercise: squat.DefaultSets ?? 4
            ));
        }

        ExerciseLibrary.ExerciseTemplate? bench = ExerciseLibrary.GetByName("Bench Press");
        if (bench != null)
        {
            exercises.Add(Exercise.CreateWithLinearProgression(
                name: bench.Name,
                category: ExerciseCategory.MainLift,
                equipment: bench.Equipment,
                assignedDay: DayNumber.Day2,
                orderInDay: 1,
                hevyExerciseTemplateId: BenchPressHevyId,
                trainingMax: TrainingMax.Create(80m, WeightUnit.Kilograms),
                useAmrap: true,
                baseSetsPerExercise: bench.DefaultSets ?? 4
            ));
        }

        ExerciseLibrary.ExerciseTemplate? deadlift = ExerciseLibrary.GetByName("Deadlift");
        if (deadlift != null)
        {
            exercises.Add(Exercise.CreateWithLinearProgression(
                name: deadlift.Name,
                category: ExerciseCategory.MainLift,
                equipment: deadlift.Equipment,
                assignedDay: DayNumber.Day3,
                orderInDay: 1,
                hevyExerciseTemplateId: DeadliftHevyId,
                trainingMax: TrainingMax.Create(120m, WeightUnit.Kilograms),
                useAmrap: true,
                baseSetsPerExercise: deadlift.DefaultSets ?? 4
            ));
        }

        ExerciseLibrary.ExerciseTemplate? ohp = ExerciseLibrary.GetByName("Overhead Press");
        if (ohp != null)
        {
            exercises.Add(Exercise.CreateWithLinearProgression(
                name: ohp.Name,
                category: ExerciseCategory.MainLift,
                equipment: ohp.Equipment,
                assignedDay: DayNumber.Day4,
                orderInDay: 1,
                hevyExerciseTemplateId: OverheadPressHevyId,
                trainingMax: TrainingMax.Create(60m, WeightUnit.Kilograms),
                useAmrap: true,
                baseSetsPerExercise: ohp.DefaultSets ?? 4
            ));
        }

        return exercises;
    }
}
