using A2S.Application.Commands.CompleteDay;
using A2S.Application.Common;
using A2S.Application.Hevy;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.GymPlanFit;

/// <summary>The extra set is bonus volume: RPS results must be identical with and without it.</summary>
public class ExtraSetProgressionTests
{
    private static readonly UserId Joe = new("user_extra_set");

    private static (Workout Workout, Exercise Split) Build()
    {
        var split = Exercise.CreateWithRepsPerSetProgression("Split Squat (Dumbbell)", ExerciseCategory.Accessory, EquipmentType.Dumbbell,
            DayNumber.Day1, 1, "20C1A3CB", RepRange.Create(8, 10), 2, 3, true, Weight.Create(16m, WeightUnit.Kilograms));
        var workout = new WorkoutBuilder().WithUserId(Joe).WithVariant(ProgramVariant.FourDay).WithExercise(split).Build();
        workout.SetExerciseExtraSetSide(split.Id, ExtraSetSide.Left);
        workout.Start();
        return (workout, split);
    }

    private static async Task<(RepsPerSetStrategy Rps, Result<CompleteDayResult> Result)> CompleteAsync(int reps, CompletedSetRequest? extra)
    {
        var (workout, split) = Build();
        var repository = Substitute.For<IWorkoutRepository>();
        repository.GetByIdAsync(workout.Id, Arg.Any<CancellationToken>()).Returns(workout);
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(Joe.Value);
        var handler = new CompleteDayCommandHandler(repository, Substitute.For<IUnitOfWork>(), user);

        var sets = Enumerable.Range(1, 2).Select(n => new CompletedSetRequest
        {
            SetNumber = n, Weight = 16m, WeightUnit = WeightUnit.Kilograms, ActualReps = reps
        }).ToList();
        var result = await handler.Handle(new CompleteDayCommand(workout.Id.Value, DayNumber.Day1,
            [new ExercisePerformanceRequest { ExerciseId = split.Id.Value, CompletedSets = sets, ExtraSet = extra }]), CancellationToken.None);

        return ((RepsPerSetStrategy)split.Progression, result);
    }

    [Theory]
    [InlineData(10, 1)]  // SUCCESS; a failed extra set must not turn it into FAILED
    [InlineData(9, 12)]  // MAINTAINED; a big extra set must not turn it into SUCCESS
    [InlineData(6, 10)]  // FAILED stays FAILED
    public async Task Rps_result_is_identical_with_and_without_the_extra_set(int reps, int extraReps)
    {
        var extra = new CompletedSetRequest { SetNumber = 3, Weight = 16m, WeightUnit = WeightUnit.Kilograms, ActualReps = extraReps };

        var without = await CompleteAsync(reps, null);
        var with = await CompleteAsync(reps, extra);

        without.Result.IsSuccess.Should().BeTrue();
        with.Result.IsSuccess.Should().BeTrue();
        with.Rps.CurrentSetCount.Should().Be(without.Rps.CurrentSetCount);
        with.Rps.CurrentWeight.Should().Be(without.Rps.CurrentWeight);
        with.Result.Value.Should().BeEquivalentTo(without.Result.Value, o => o.Excluding(m => m.Name == "ExerciseId"));
    }

    [Fact]
    public void Routine_notes_say_left_side_first_and_name_the_extra_set()
    {
        var (_, split) = Build();

        RoutineBuilder.SideNotes(split).Should().Equal("Left side first", "Extra set: left");
    }
}
