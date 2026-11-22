using A2S.Application.Cardio;
using A2S.Application.Common;
using A2S.Application.Queries.GetCycle;
using A2S.Application.Queries.GetWeekPlan;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using MediatR;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries;

public class PlannedCardioTests
{
    private static readonly UserId Joe = new("user_planned_cardio");
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);
    private static readonly DateTimeOffset At = new(2026, 11, 16, 19, 0, 0, TimeSpan.Zero);
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly ICardioTrackRepository _tracks = Substitute.For<ICardioTrackRepository>();
    private readonly ICardioProtocolRepository _protocols = Substitute.For<ICardioProtocolRepository>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly Workout _workout;
    private readonly CardioTrack _stairs;
    private readonly MobilityRoutineId _hips = new(Guid.Parse("11111111-2222-3333-4444-555555555555"));
    private readonly GetWeekPlanQueryHandler _weekPlan;

    public PlannedCardioTests()
    {
        var bench = Exercise.CreateWithLinearProgression("Bench Press (Barbell)", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day1, 1, "79D0BB3A", TrainingMax.Create(100m, WeightUnit.Kilograms));
        var squat = Exercise.CreateWithLinearProgression("Squat (Barbell)", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day2, 1, "D04AC939", TrainingMax.Create(120m, WeightUnit.Kilograms));
        _workout = new WorkoutBuilder().WithUserId(Joe).WithVariant(ProgramVariant.FourDay).WithExercise(bench).WithExercise(squat).Build();
        _workout.Start();
        _workouts.GetByIdAsync(_workout.Id, Arg.Any<CancellationToken>()).Returns(_workout);
        _workouts.GetActiveWorkoutAsync(Joe, Arg.Any<CancellationToken>()).Returns(_workout);

        _stairs = CardioTrackTemplates.Create(CardioTrackTemplates.StairFinisher, Joe, Mon16Nov);
        _tracks.ListForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns([_stairs]);
        _protocols.ListForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns([]);

        var plan = TrainingPlan.Create(Joe, "Plan", Mon16Nov,
        [
            ScheduledSession.Lift(DayOfWeek.Monday, DayNumber.Day1, "Upper A", finisher: SessionFinisher.ForTrack(_stairs.Id)),
            ScheduledSession.Mobility(DayOfWeek.Monday, _hips, "Hips & lower back"),
            ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day2, "Lower A",
                finisher: SessionFinisher.ForFixed(FixedActivity.EasyInclineTreadmill()))
        ], 95m);
        plan.LinkWorkout(_workout.Id);
        _plans.GetActiveForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(plan);
        _user.UserId.Returns(Joe.Value);
        _weekPlan = new GetWeekPlanQueryHandler(_workouts, _user, new A2SProgramProvider(), _plans, _tracks, _protocols);
    }

    [Fact]
    public async Task Week_plan_lists_the_track_finisher_after_the_exercises_with_its_prescription_and_progression()
    {
        var plan = (await _weekPlan.Handle(new GetWeekPlanQuery(_workout.Id.Value, 1, 1), CancellationToken.None)).Value;

        plan.SessionLabel.Should().Be("Upper A");
        plan.PairedRoutineId.Should().Be(_hips.Value);
        plan.IsCurrent.Should().BeTrue();
        var cardio = plan.Cardio!;
        cardio.Kind.Should().Be("Track");
        cardio.Name.Should().Be("Stair finisher");
        cardio.Prescription.Should().Be("5 min continuous @ level 6");
        cardio.ProgressionType.Should().Be("Cardio track: double progression");
        cardio.OrderInDay.Should().Be(plan.Exercises.Count + 1);
        cardio.Minutes.Should().Be(5);
        cardio.IntensityUnit.Should().Be("Level");
        cardio.Intensity.Should().Be(6);
        cardio.SessionsToStepUp.Should().Be(2);
        cardio.StepUpNote.Should().Be("2 more sessions to step up");
        cardio.CardioTrackId.Should().Be(_stairs.Id.Value);
    }

    [Fact]
    public async Task Week_plan_lists_a_fixed_cool_down_as_fixed()
    {
        var plan = (await _weekPlan.Handle(new GetWeekPlanQuery(_workout.Id.Value, 1, 2), CancellationToken.None)).Value;

        plan.Cardio!.Kind.Should().Be("Fixed");
        plan.Cardio.ProgressionType.Should().Be("Fixed");
        plan.Cardio.Prescription.Should().Be("10 min · Easy incline");
        plan.Cardio.StepUpNote.Should().BeNull();
        plan.IsCurrent.Should().BeFalse();
    }

    [Fact]
    public async Task Week_plan_without_plan_repositories_has_no_cardio()
    {
        var bare = new GetWeekPlanQueryHandler(_workouts, _user, new A2SProgramProvider());

        var plan = (await bare.Handle(new GetWeekPlanQuery(_workout.Id.Value, 1, 1), CancellationToken.None)).Value;

        plan.Cardio.Should().BeNull();
        plan.SessionLabel.Should().BeNull();
    }

    [Fact]
    public async Task Cycle_starts_at_the_current_session_then_lists_the_rest_of_this_week_and_next_week()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetWeekPlanQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => _weekPlan.Handle(call.Arg<GetWeekPlanQuery>(), CancellationToken.None));
        var handler = new GetCycleQueryHandler(_workouts, _user, sender);

        var cycle = (await handler.Handle(new GetCycleQuery(null), CancellationToken.None)).Value;

        cycle.CurrentWeek.Should().Be(1);
        cycle.DaysPerWeek.Should().Be(4);
        cycle.Sessions.Select(s => (s.Week, s.Day)).Should().Equal((1, 1), (1, 2), (1, 3), (1, 4), (2, 1), (2, 2), (2, 3), (2, 4));
        cycle.Sessions[0].IsCurrent.Should().BeTrue();
        cycle.Sessions[0].Label.Should().Be("Upper A");
        cycle.Sessions[0].Plan.Cardio!.Name.Should().Be("Stair finisher");
        cycle.Sessions.Skip(1).Should().OnlyContain(s => !s.IsCurrent);
    }

    [Fact]
    public void Evaluation_outcome_reads_like_a_lift_change_when_the_track_steps_up()
    {
        var before = _stairs.Prescription.Title;
        var log = new CardioLog(300, null, 7, null, null, effort: EffortRating.Solid);
        _stairs.LogSession("a", Mon16Nov, log, true, null, At);
        var second = _stairs.LogSession("b", Mon16Nov.AddDays(3), log, true, null, At);

        var outcome = PlannedCardioBuilder.ForEvaluation(_stairs, before, second);

        outcome.Outcome.Should().Be("SteppedUp");
        outcome.Progressed.Should().BeTrue();
        outcome.Summary.Should().Be("Stair finisher: 5 min continuous @ level 6 → 6 min continuous @ level 6");
        outcome.NextTime.Should().Be("Next time: 6 min continuous @ level 6");
    }
}
