using A2S.Application.Common;
using A2S.Application.Queries.GetDayBudget;
using A2S.Application.Services;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries;

public class GetDayBudgetQueryHandlerTests
{
    private static readonly UserId Owner = new("dd011111-1111-1111-1111-111111111111");
    private static readonly UserId Stranger = new("dd022222-2222-2222-2222-222222222222");
    private static readonly Guid WorkoutGuid = Guid.Parse("dd033333-3333-3333-3333-333333333333");

    private readonly IWorkoutRepository _workoutRepository = Substitute.For<IWorkoutRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IExerciseLibraryProvider _library = Substitute.For<IExerciseLibraryProvider>();
    private readonly GetDayBudgetQueryHandler _handler;

    public GetDayBudgetQueryHandlerTests()
    {
        _library.GetAllTemplatesAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new ExerciseTemplate("Bench Press (Barbell)", EquipmentType.Barbell, MuscleGroup: "chest", SecondaryMuscleGroups: ["triceps", "shoulders"]),
            new ExerciseTemplate("Cable Fly", EquipmentType.Cable, MuscleGroup: "chest"),
            new ExerciseTemplate("Pec Deck (Machine)", EquipmentType.Machine, MuscleGroup: "chest"),
            new ExerciseTemplate("Squat (Barbell)", EquipmentType.Barbell, MuscleGroup: "quadriceps", SecondaryMuscleGroups: ["glutes", "hamstrings"])
        });
        var calculator = new DayBudgetCalculator(new VolumeBudgetService(new A2SProgramProvider()), _library);
        _handler = new GetDayBudgetQueryHandler(_workoutRepository, _currentUserService, calculator);
    }

    [Fact]
    public async Task Handle_WhenNotAuthenticated_ReturnsUnauthenticated()
    {
        _currentUserService.UserId.Returns((string?)null);

        var result = await _handler.Handle(new GetDayBudgetQuery(WorkoutGuid, DayNumber.Day1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
    }

    [Fact]
    public async Task Handle_WhenWorkoutMissing_ReturnsNotFound()
    {
        AuthenticateAs(Owner);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>()).Returns((Workout?)null);

        var result = await _handler.Handle(new GetDayBudgetQuery(WorkoutGuid, DayNumber.Day1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task Handle_WhenWorkoutBelongsToAnotherUser_ReturnsUnauthorized()
    {
        AuthenticateAs(Stranger);
        GivenWorkout(UpperAndLower());

        var result = await _handler.Handle(new GetDayBudgetQuery(WorkoutGuid, DayNumber.Day1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.Unauthorized);
    }

    [Fact]
    public async Task Handle_ReturnsWeeklySetsAcrossTheWorkout_AndTimeForTheRequestedDay()
    {
        AuthenticateAs(Owner);
        GivenWorkout(UpperAndLower());

        var result = await _handler.Handle(new GetDayBudgetQuery(WorkoutGuid, DayNumber.Day1, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var budget = result.Value;
        budget.HardSetsThisWeek.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["Chest"] = 5,
            ["Shoulders"] = 3,
            ["Arms"] = 3,
            ["Quads"] = 5,
            ["Hamstrings"] = 3,
            ["Glutes"] = 3
        });
        budget.VolumeWarnings.Should().BeEmpty();
        budget.Time.WarmUpMinutes.Should().Be(10);
        budget.Time.TotalMinutes.Should().Be(29, "5 × (40 + 180) s = 1100 s → 19 min, plus the warm-up");
        budget.Time.PerExercise.Should().ContainSingle().Which.Name.Should().Be("Bench Press (Barbell)");
        budget.Time.OverBudget.Should().BeFalse();
        budget.Time.BudgetMinutes.Should().Be(55);
    }

    [Fact]
    public async Task Handle_DefaultsToTheWorkoutsCurrentWeek()
    {
        AuthenticateAs(Owner);
        GivenWorkout(UpperAndLower());

        var defaulted = await _handler.Handle(new GetDayBudgetQuery(WorkoutGuid, DayNumber.Day1), CancellationToken.None);
        var explicitWeekOne = await _handler.Handle(new GetDayBudgetQuery(WorkoutGuid, DayNumber.Day1, 1), CancellationToken.None);

        defaulted.Value.Should().BeEquivalentTo(explicitWeekOne.Value);
    }

    [Fact]
    public async Task Handle_OnADeloadWeek_CountsFourSetsForTheMainLift()
    {
        AuthenticateAs(Owner);
        GivenWorkout(UpperAndLower());

        var result = await _handler.Handle(new GetDayBudgetQuery(WorkoutGuid, DayNumber.Day1, 7), CancellationToken.None);

        result.Value.HardSetsThisWeek["Chest"].Should().Be(4);
    }

    [Fact]
    public async Task Handle_WhenChestGoesOverTheCap_ReturnsOneWarningAndStillSucceeds()
    {
        AuthenticateAs(Owner);
        var workout = new WorkoutBuilder().WithUserId(Owner).WithVariant(ProgramVariant.FourDay)
            .WithExercise(e => e.WithName("Bench Press (Barbell)").WithOrder(1).AsLinear())
            .WithExercise(e => e.WithName("Cable Fly").WithCategory(ExerciseCategory.Accessory).WithEquipment(EquipmentType.Cable).WithOrder(2).AsRepsPerSet(startingSets: 4))
            .WithExercise(e => e.WithName("Pec Deck (Machine)").WithCategory(ExerciseCategory.Accessory).WithEquipment(EquipmentType.Machine).WithOrder(3).AsRepsPerSet(startingSets: 3))
            .Build();
        GivenWorkout(workout);

        var result = await _handler.Handle(new GetDayBudgetQuery(WorkoutGuid, DayNumber.Day1, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.VolumeWarnings.Should().ContainSingle();
        result.Value.VolumeWarnings[0].Bucket.Should().Be("Chest");
        result.Value.VolumeWarnings[0].Sets.Should().Be(12);
        result.Value.VolumeWarnings[0].Message.Should().Be("Chest: 12 hard sets this week, cap 10");
    }

    [Fact]
    public async Task Handle_DoublesSetsForUnilateralExercisesInTheTimeEstimate()
    {
        AuthenticateAs(Owner);
        var workout = new WorkoutBuilder().WithUserId(Owner).WithVariant(ProgramVariant.FourDay)
            .WithExercise(e => e.WithName("Split Squat").WithCategory(ExerciseCategory.Accessory).AsRepsPerSet(startingSets: 3, isUnilateral: true))
            .Build();
        GivenWorkout(workout);

        var result = await _handler.Handle(new GetDayBudgetQuery(WorkoutGuid, DayNumber.Day1, 1), CancellationToken.None);

        result.Value.Time.PerExercise.Should().ContainSingle().Which.Minutes.Should().Be(13, "6 sets × (40 + 90) s = 780 s");
    }

    [Fact]
    public async Task Handle_WhenWeekOutOfRange_ReturnsValidationFailed()
    {
        AuthenticateAs(Owner);
        GivenWorkout(UpperAndLower());

        var result = await _handler.Handle(new GetDayBudgetQuery(WorkoutGuid, DayNumber.Day1, 22), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.ValidationFailed);
    }

    [Fact]
    public async Task Handle_WhenDayBeyondTheVariant_ReturnsValidationFailed()
    {
        AuthenticateAs(Owner);
        GivenWorkout(UpperAndLower());

        var result = await _handler.Handle(new GetDayBudgetQuery(WorkoutGuid, DayNumber.Day5, 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.ValidationFailed);
    }

    [Fact]
    public async Task Handle_DayWithNoExercises_IsJustTheWarmUp()
    {
        AuthenticateAs(Owner);
        GivenWorkout(UpperAndLower());

        var result = await _handler.Handle(new GetDayBudgetQuery(WorkoutGuid, DayNumber.Day3, 1), CancellationToken.None);

        result.Value.Time.TotalMinutes.Should().Be(10);
        result.Value.Time.PerExercise.Should().BeEmpty();
    }

    private void AuthenticateAs(UserId userId) => _currentUserService.UserId.Returns(userId.Value);

    private void GivenWorkout(Workout workout) =>
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>()).Returns(workout);

    /// <summary>Bench on day 1 and squat on day 2 of a four-day program; days 3 and 4 are empty.</summary>
    private static Workout UpperAndLower() =>
        new WorkoutBuilder().WithUserId(Owner).WithVariant(ProgramVariant.FourDay)
            .WithExercise(e => e.WithName("Bench Press (Barbell)").WithDay(DayNumber.Day1).AsLinear())
            .WithExercise(e => e.WithName("Squat (Barbell)").WithDay(DayNumber.Day2).AsLinear())
            .Build();
}
