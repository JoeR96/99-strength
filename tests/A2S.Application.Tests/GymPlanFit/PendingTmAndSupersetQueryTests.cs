using A2S.Application.Commands.SetTrainingMax;
using A2S.Application.Common;
using A2S.Application.Queries.GetWeekPlan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.GymPlanFit;

public class PendingTmAndSupersetQueryTests
{
    private static readonly UserId Joe = new("user_pending_tm");
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly Workout _workout;
    private readonly Exercise _incline;
    private readonly Exercise _pulldown;
    private readonly Exercise _facePull;

    public PendingTmAndSupersetQueryTests()
    {
        _incline = Exercise.CreateWithPendingTrainingMax("Incline Bench Press (Smith Machine)", ExerciseCategory.MainLift,
            EquipmentType.Machine, DayNumber.Day1, 1, "3A6FA3D1", tier: ProgramTier.Auxiliary);
        _pulldown = new ExerciseBuilder().WithName("Lat Pulldown Neutral Grip").WithCategory(ExerciseCategory.Accessory).WithOrder(2).AsRepsPerSet(8, 12, 3, 5).Build();
        _facePull = new ExerciseBuilder().WithName("Face Pull").WithCategory(ExerciseCategory.Accessory).WithOrder(3).AsRepsPerSet(12, 15, 2, 4).Build();
        _workout = new WorkoutBuilder().WithUserId(Joe).WithVariant(ProgramVariant.FourDay)
            .WithExercise(_incline).WithExercise(_pulldown).WithExercise(_facePull).Build();
        _workout.SetExerciseSupersetGroup(_pulldown.Id, 2);
        _workout.SetExerciseSupersetGroup(_facePull.Id, 2);
        _workout.Start();
        _workouts.GetByIdAsync(_workout.Id, Arg.Any<CancellationToken>()).Returns(_workout);
        _user.UserId.Returns(Joe.Value);
    }

    [Fact]
    public async Task Week_plan_shows_pending_tm_with_reps_and_intensity_and_superset_groups_with_superset_rest()
    {
        var handler = new GetWeekPlanQueryHandler(_workouts, _user, new A2SProgramProvider());

        var plan = (await handler.Handle(new GetWeekPlanQuery(_workout.Id.Value, 1, 1), CancellationToken.None)).Value;

        var incline = plan.Exercises.Single(e => e.Name.StartsWith("Incline"));
        incline.Metadata.IsTrainingMaxPending.Should().BeTrue();
        incline.Metadata.Tier.Should().Be("Auxiliary");
        incline.Metadata.IntensityPercentage.Should().Be(A2SHypertrophyProgram.GetWeekData(1, ProgramTier.Auxiliary).Intensity);
        incline.PlannedSets.Should().OnlyContain(s => s.WeightKg == 0m && s.TargetReps > 0);
        var pair = plan.Exercises.Where(e => e.SupersetGroup == 2).ToList();
        pair.Select(e => e.Name).Should().Equal("Lat Pulldown Neutral Grip", "Face Pull");
        pair.Should().OnlyContain(e => e.RestSeconds == RestDefaults.SupersetSeconds);
    }

    [Fact]
    public async Task Set_training_max_from_a_recent_set_clears_pending()
    {
        var handler = new SetTrainingMaxCommandHandler(_workouts, Substitute.For<IUnitOfWork>(), _user);

        var result = await handler.Handle(new SetTrainingMaxCommand(_workout.Id.Value, _incline.Id.Value, null, 60m, 8, WeightUnit.Kilograms), CancellationToken.None);

        result.Value.Should().Be(new SetTrainingMaxResult(_incline.Id.Value, 67.5m, "Kilograms", true));
        _incline.IsTrainingMaxPending.Should().BeFalse();
    }

    [Fact]
    public async Task Set_training_max_refuses_a_non_a2s2_exercise()
    {
        var handler = new SetTrainingMaxCommandHandler(_workouts, Substitute.For<IUnitOfWork>(), _user);

        var result = await handler.Handle(new SetTrainingMaxCommand(_workout.Id.Value, _facePull.Id.Value, 50m, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validator_needs_either_a_tm_or_a_recent_set()
    {
        var validator = new SetTrainingMaxCommandValidator();

        validator.Validate(new SetTrainingMaxCommand(Guid.NewGuid(), Guid.NewGuid(), null, null, null)).IsValid.Should().BeFalse();
        validator.Validate(new SetTrainingMaxCommand(Guid.NewGuid(), Guid.NewGuid(), 100m, 80m, 5)).IsValid.Should().BeFalse();
        validator.Validate(new SetTrainingMaxCommand(Guid.NewGuid(), Guid.NewGuid(), 100m, null, null)).IsValid.Should().BeTrue();
    }
}
