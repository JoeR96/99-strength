using System.Net;
using System.Net.Http.Json;
using A2S.Api.Contracts.Requests;
using A2S.Application.Commands.SuggestedPlans;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Services;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// Phase 7 onboarding end to end: preview → start → Today shows the plan; a second start is a no-op;
/// the workout and day budget exist for the seeded exercises. Runs against the real seeded library
/// (ExerciseDefinitionSeeder on boot), so every template name in the seed must resolve.
/// </summary>
[Collection("Integration")]
public class SuggestedPlansControllerTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public SuggestedPlansControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static StartSuggestedPlanRequest Body() => new(100m, 140m, 60m, 180m, 95m);

    [Fact]
    public async Task GetSuggested_PreviewsTheCutTo85Plan()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var previews = await client.GetFromJsonAsync<List<SuggestedPlanPreviewDto>>("/api/v1/plans/suggested");

        var preview = previews.Should().ContainSingle().Subject;
        preview.Key.Should().Be("cut-to-85");
        preview.StartDate.DayOfWeek.Should().Be(DayOfWeek.Monday);
        preview.StartDate.Should().BeOnOrAfter(DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1)));
        preview.Inputs.Should().HaveCount(5);
        preview.LiftingDays.Should().HaveCount(4);
        preview.AlreadyStarted.Should().BeFalse();
    }

    [Fact]
    public async Task Start_CreatesThePlanWorkoutProtocolsRoutinesAndHabits_AndTodayShowsIt()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync("/api/v1/plans/suggested/cut-to-85", Body());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = (await response.Content.ReadFromJsonAsync<StartSuggestedPlanResult>())!;
        result.AlreadyStarted.Should().BeFalse();
        result.StartDate.DayOfWeek.Should().Be(DayOfWeek.Monday);
        result.WorkoutId.Should().NotBeNull();
        result.Warnings.Should().BeEmpty();

        var plan = (await client.GetFromJsonAsync<TrainingPlanDto>("/api/v1/plans/active"))!;
        plan.Id.Should().Be(result.PlanId);
        plan.Name.Should().Be("Cut to 85 — Nov 2026");
        plan.LinkedWorkoutId.Should().Be(result.WorkoutId);
        plan.Schedule.Should().HaveCount(8);
        plan.Phases.Should().HaveCountGreaterThanOrEqualTo(1);
        plan.StartWeightKg.Should().Be(95m);

        var workout = (await client.GetFromJsonAsync<WorkoutDto>($"/api/v1/workouts/{result.WorkoutId}"))!;
        workout.Exercises.Should().HaveCount(20);

        var protocols = await client.GetFromJsonAsync<List<CardioProtocolDto>>("/api/v1/cardio-protocols");
        protocols.Should().HaveCount(3);
        var routines = await client.GetFromJsonAsync<List<MobilityRoutineDto>>("/api/v1/mobility-routines");
        routines!.Select(r => r.Name).Should().BeEquivalentTo("Mobility A", "Mobility B");
        var habits = await client.GetFromJsonAsync<List<HabitDto>>("/api/v1/habits");
        habits.Should().HaveCount(5);

        var monday = await client.GetFromJsonAsync<TodayDto>($"/api/v1/plans/today?date={result.StartDate:yyyy-MM-dd}");
        monday!.Sessions.Should().ContainSingle();
        monday.Sessions[0].Label.Should().Be("Upper A");
        monday.Sessions[0].Status.Should().Be(SessionStatus.Planned);
        monday.Phase.Should().Be(PhaseType.Cut);
        monday.Week.LiftsPlanned.Should().Be(4);
        monday.Week.CardioPlanned.Should().Be(2);
        monday.Week.MobilityPlanned.Should().Be(1);

        var wednesday = await client.GetFromJsonAsync<TodayDto>($"/api/v1/plans/today?date={result.StartDate.AddDays(2):yyyy-MM-dd}");
        wednesday!.Sessions.Select(s => s.Kind).Should().BeEquivalentTo([SessionKind.Cardio, SessionKind.Mobility]);
        wednesday.Sessions.Should().OnlyContain(s => s.CardioProtocolId != null || s.MobilityRoutineId != null);
    }

    [Fact]
    public async Task Start_Twice_ReturnsTheSamePlanAndCreatesNothingElse()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var first = await (await client.PostAsJsonAsync("/api/v1/plans/suggested/cut-to-85", Body())).Content.ReadFromJsonAsync<StartSuggestedPlanResult>();
        var secondResponse = await client.PostAsJsonAsync("/api/v1/plans/suggested/cut-to-85", Body());
        var second = await secondResponse.Content.ReadFromJsonAsync<StartSuggestedPlanResult>();

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        second!.AlreadyStarted.Should().BeTrue();
        second.PlanId.Should().Be(first!.PlanId);
        second.WorkoutId.Should().Be(first.WorkoutId);

        (await client.GetFromJsonAsync<List<WorkoutSummaryDto>>("/api/v1/workouts"))!.Should().ContainSingle();
        (await client.GetFromJsonAsync<List<HabitDto>>("/api/v1/habits"))!.Should().HaveCount(5);
        (await client.GetFromJsonAsync<List<SuggestedPlanPreviewDto>>("/api/v1/plans/suggested"))![0].AlreadyStarted.Should().BeTrue();
    }

    [Fact]
    public async Task Start_WithATrainingMaxOutOfRange_IsBadRequest()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync("/api/v1/plans/suggested/cut-to-85", new StartSuggestedPlanRequest(10m, 140m, 60m, 180m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Start_Anonymous_IsUnauthorized()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/v1/plans/suggested/cut-to-85", Body());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
