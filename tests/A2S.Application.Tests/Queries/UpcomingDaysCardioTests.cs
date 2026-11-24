using System.Text.Json;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Queries.GetWeekPlan;
using A2S.Application.Queries.Plans;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries;

/// <summary>
/// Upcoming days (read by Terran CC through a personal API token) keep a cardio exercise in a lift's list,
/// with no sets and the block text in the additive <c>cardio</c> field. Existing fields do not change.
/// </summary>
public class UpcomingDaysCardioTests
{
    private static readonly UserId TestUserId = new("user_upcoming_cardio_0001");
    private static readonly DateOnly Mon2Nov = new(2026, 11, 2);

    private readonly ITrainingPlanRepository _plans = Substitute.For<ITrainingPlanRepository>();
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly IHevyLinkRepository _hevyLinks = Substitute.For<IHevyLinkRepository>();
    private readonly ICardioProtocolRepository _protocols = Substitute.For<ICardioProtocolRepository>();
    private readonly ICardioTrackRepository _tracks = Substitute.For<ICardioTrackRepository>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly GetUpcomingDaysQueryHandler _handler;

    public UpcomingDaysCardioTests()
    {
        var clock = new LocalClock(new FakeTimeProvider(new DateTimeOffset(2026, 11, 22, 9, 0, 0, TimeSpan.Zero)));
        _hevyLinks.ListAsync(Arg.Any<UserId>(), Arg.Any<HevyLinkKind>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<HevyLink>());
        _currentUser.UserId.Returns(TestUserId.Value);
        var weekPlan = new GetWeekPlanQueryHandler(_workouts, _currentUser, new A2SProgramProvider());
        _sender.Send(Arg.Any<GetWeekPlanQuery>(), Arg.Any<CancellationToken>())
            .Returns(ci => weekPlan.Handle(ci.Arg<GetWeekPlanQuery>(), CancellationToken.None));
        _handler = new GetUpcomingDaysQueryHandler(_plans, _workouts, _hevyLinks, _protocols, _tracks, _currentUser, new PlanCalendarService(clock), clock, _sender);
    }

    [Fact]
    public async Task Upcoming_KeepsCardioExercise_WithText()
    {
        var plan = TrainingPlan.Create(TestUserId, "Plan", Mon2Nov,
            [ScheduledSession.Lift(DayOfWeek.Tuesday, DayNumber.Day1, "Upper A")], 95m);
        _plans.GetActiveForUserAsync(TestUserId, Arg.Any<CancellationToken>()).Returns(plan);
        var workout = new WorkoutBuilder().WithUserId(TestUserId).WithVariant(ProgramVariant.FourDay)
            .WithExercise(e => e.WithName("Bench").WithDay(DayNumber.Day1).WithOrder(1).AsLinear(trainingMax: 100m, baseSets: 4))
            .WithExercise(e => e.WithDay(DayNumber.Day1).WithOrder(2).AsCardioStage(CardioLane.Hard))
            .Build();
        workout.Start();
        plan.LinkWorkout(workout.Id);
        _workouts.GetByIdAsync(workout.Id, Arg.Any<CancellationToken>()).Returns(workout);

        var result = await _handler.Handle(new GetUpcomingDaysQuery(null, 3), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var exercises = result.Value[2].Lifts.Single().Exercises;
        exercises.Select(e => e.Name).Should().Equal("Bench", "Stair Machine (Floors)");
        exercises[0].Sets.Should().NotBeEmpty();
        exercises[0].Cardio.Should().BeNull();
        exercises[1].Should().BeEquivalentTo(new UpcomingExerciseDto(
            "Stair Machine (Floors)", "Cardio", [], false, null,
            "3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)"));
    }

    [Fact]
    public void UpcomingExercise_Json_KeepsTheExistingFieldsAndAddsCardio()
    {
        var json = JsonSerializer.Serialize(
            new UpcomingExerciseDto("Bench", "MainLift", [new UpcomingSetDto(75m, 5, false)], true, 0.7m),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Be("""{"name":"Bench","category":"MainLift","sets":[{"weightKg":75,"reps":5,"isAmrap":false}],"isTrainingMaxPending":true,"intensityPercentage":0.7,"cardio":null}""");
    }
}
