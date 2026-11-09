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
/// Creates a new workout program from configured exercises (via
/// <see cref="ConfiguredExerciseFactory"/>) or the default main lifts for the variant.
/// </summary>
public sealed class CreateWorkoutCommandHandler : IRequestHandler<CreateWorkoutCommand, Result<Guid>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IExerciseLibraryProvider _exerciseLibrary;

    public CreateWorkoutCommandHandler(
        IWorkoutRepository workoutRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IExerciseLibraryProvider exerciseLibrary)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _exerciseLibrary = exerciseLibrary ?? throw new ArgumentNullException(nameof(exerciseLibrary));
    }

    public async Task<Result<Guid>> Handle(CreateWorkoutCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            if (userId == null)
            {
                return Result.Failure<Guid>("User must be authenticated to create a workout.");
            }

            var existingWorkout = await _workoutRepository.GetActiveWorkoutAsync(userId.Value, cancellationToken);
            if (existingWorkout != null)
            {
                return Result.Failure<Guid>("An active workout already exists. Complete or pause it before creating a new one.");
            }

            var exercises = request.Exercises != null && request.Exercises.Count > 0
                ? ConfiguredExerciseFactory.Create(_exerciseLibrary, request.Exercises)
                : CreateExercisesForVariant(request.Variant);

            var workout = Workout.Create(
                userId.Value,
                request.Name,
                request.Variant,
                exercises,
                request.BlockSequence);

            workout.Start();

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
    /// Creates default exercises based on the program variant.
    /// Creates a basic program with the 4 main lifts using Linear progression.
    /// </summary>
    private List<Exercise> CreateExercisesForVariant(ProgramVariant variant)
    {
        List<Exercise> exercises = new List<Exercise>();

        // Default Hevy exercise template IDs for main lifts
        const string SquatHevyId = "D04AC939";
        const string BenchPressHevyId = "79D0BB3A";
        const string DeadliftHevyId = "C6272009";
        const string OverheadPressHevyId = "7B8D84E8";

        var squat = _exerciseLibrary.GetByName("Squat (Barbell)");
        if (squat != null)
        {
            exercises.Add(Exercise.CreateWithLinearProgression(
                name: squat.Name,
                category: ExerciseCategory.MainLift,
                equipment: squat.Equipment,
                assignedDay: DayNumber.Day1,
                orderInDay: 1,
                externalTemplateId: SquatHevyId,
                trainingMax: TrainingMax.Create(100m, WeightUnit.Kilograms),
                useAmrap: true,
                baseSetsPerExercise: squat.DefaultSets ?? 4
            ));
        }

        var bench = _exerciseLibrary.GetByName("Bench Press (Barbell)");
        if (bench != null)
        {
            exercises.Add(Exercise.CreateWithLinearProgression(
                name: bench.Name,
                category: ExerciseCategory.MainLift,
                equipment: bench.Equipment,
                assignedDay: DayNumber.Day2,
                orderInDay: 1,
                externalTemplateId: BenchPressHevyId,
                trainingMax: TrainingMax.Create(80m, WeightUnit.Kilograms),
                useAmrap: true,
                baseSetsPerExercise: bench.DefaultSets ?? 4
            ));
        }

        var deadlift = _exerciseLibrary.GetByName("Deadlift (Barbell)");
        if (deadlift != null)
        {
            exercises.Add(Exercise.CreateWithLinearProgression(
                name: deadlift.Name,
                category: ExerciseCategory.MainLift,
                equipment: deadlift.Equipment,
                assignedDay: DayNumber.Day3,
                orderInDay: 1,
                externalTemplateId: DeadliftHevyId,
                trainingMax: TrainingMax.Create(120m, WeightUnit.Kilograms),
                useAmrap: true,
                baseSetsPerExercise: deadlift.DefaultSets ?? 4
            ));
        }

        var ohp = _exerciseLibrary.GetByName("Overhead Press (Barbell)");
        if (ohp != null)
        {
            exercises.Add(Exercise.CreateWithLinearProgression(
                name: ohp.Name,
                category: ExerciseCategory.MainLift,
                equipment: ohp.Equipment,
                assignedDay: DayNumber.Day4,
                orderInDay: 1,
                externalTemplateId: OverheadPressHevyId,
                trainingMax: TrainingMax.Create(60m, WeightUnit.Kilograms),
                useAmrap: true,
                baseSetsPerExercise: ohp.DefaultSets ?? 4
            ));
        }

        return exercises;
    }
}
