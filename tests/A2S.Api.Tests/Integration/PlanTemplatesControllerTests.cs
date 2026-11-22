using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using A2S.Api.Contracts.Requests;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.GymSessions;
using A2S.Application.Commands.SetTrainingMax;
using A2S.Application.Commands.SuggestedPlans;
using A2S.Application.DTOs;
using A2S.Application.Queries.GetWeekPlan;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Infrastructure.Persistence;
using A2S.Tests.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// Plan templates end to end for a signed-in user, on the real seeded exercise library: list, preview
/// Plan v3, apply it with pending training maxes, set a TM, finish a lower day with the 10-minute
/// cool-down, and replace an existing plan only after confirmation, keeping its history.
/// </summary>
[Collection("Integration")]
public class PlanTemplatesControllerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly TestWebApplicationFactory<Program> _factory;

    public PlanTemplatesControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static ApplyPlanTemplateRequest PlanV3(bool confirm = false) =>
        new(null, new Dictionary<string, decimal?>(), null, confirm);

    [Fact]
    public async Task Lists_both_templates_and_previews_plan_v3_from_a_chosen_monday()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var all = await client.GetFromJsonAsync<List<SuggestedPlanPreviewDto>>("/api/v1/plans/templates");
        var suggested = await client.GetFromJsonAsync<List<SuggestedPlanPreviewDto>>("/api/v1/plans/suggested");
        var preview = await client.GetFromJsonAsync<SuggestedPlanPreviewDto>("/api/v1/plans/templates/plan-v3?startDate=2030-01-09");

        all!.Select(t => t.Key).Should().Equal("cut-to-85", "plan-v3");
        suggested!.Select(t => t.Key).Should().Equal("cut-to-85", "plan-v3");
        preview!.StartDate.Should().Be(new DateOnly(2030, 1, 14));
        preview.Phases.Should().HaveCount(3);
        preview.Sessions.Should().HaveCount(4);
        preview.Slots.Should().HaveCount(13);
        preview.Routines.Should().HaveCount(5);
        preview.CardioTracks.Should().HaveCount(3);
        preview.ToFillIn.Should().Contain(i => i.Contains("Pause Squat (Barbell)"));
        preview.Inputs.Where(i => i.Key.EndsWith("Tm")).Should().OnlyContain(i => i.Optional);
        (await client.GetAsync("/api/v1/plans/templates/nope")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Apply_with_pending_tms_then_set_a_tm_and_finish_lower_a_with_the_cool_down()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync("/api/v1/plans/templates/plan-v3/apply", PlanV3());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var applied = (await response.Content.ReadFromJsonAsync<StartSuggestedPlanResult>())!;
        var again = await client.PostAsJsonAsync("/api/v1/plans/templates/plan-v3/apply", PlanV3());
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        (await again.Content.ReadFromJsonAsync<StartSuggestedPlanResult>())!.AlreadyStarted.Should().BeTrue();

        var plan = (await client.GetFromJsonAsync<TrainingPlanDto>("/api/v1/plans/active"))!;
        plan.Name.Should().Be("Plan v3 — Base/Build/Transition");
        plan.Schedule.Single(s => s.Label == "Lower A").FinisherFixed.Should().Be(new FixedActivityDto("Treadmill", 10, "Easy incline"));
        plan.Schedule.Single(s => s.Label == "Upper A").FinisherCardioTrackId.Should().NotBeNull();

        var workoutId = applied.WorkoutId!.Value;
        var lowerA = (await client.GetFromJsonAsync<WeekPlanDto>($"/api/v1/workouts/{workoutId}/weeks/1/days/2/plan"))!;
        var pause = lowerA.Exercises.Single(e => e.Name == "Pause Squat (Barbell)");
        pause.Metadata.IsTrainingMaxPending.Should().BeTrue();
        pause.PlannedSets.Should().OnlyContain(s => s.WeightKg == 0m);
        lowerA.Exercises.Where(e => e.SupersetGroup == 3).Select(e => e.Name).Should().Equal("Split Squat (Dumbbell)", "Hip Abduction (Machine)");

        var tuesday = applied.StartDate.AddDays(1);
        var started = new DateTimeOffset(tuesday.ToDateTime(new TimeOnly(18, 0)), TimeSpan.Zero);
        FinishGymSessionCommand Finish(WeekPlanDto day) => new(Guid.NewGuid(), workoutId, DayNumber.Day2, started, started.AddMinutes(60),
            [
                new ExercisePerformanceRequest
                {
                    ExerciseId = pause.ExerciseId,
                    CompletedSets = day.Exercises.Single(e => e.ExerciseId == pause.ExerciseId).PlannedSets.Select(s => new CompletedSetRequest
                    {
                        SetNumber = s.SetNumber, Weight = 80m, WeightUnit = WeightUnit.Kilograms, ActualReps = s.TargetReps, WasAmrap = s.IsAmrap
                    }).ToList()
                }
            ],
            Finisher: new FinisherLogRequest(600, Effort.Easy));

        var blocked = await client.PostAsJsonAsync("/api/v1/gym-sessions", Finish(lowerA), Json);
        blocked.IsSuccessStatusCode.Should().BeFalse();
        (await blocked.Content.ReadAsStringAsync()).Should().Contain("Set your training max for Pause Squat (Barbell)");

        var tm = await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/exercises/{pause.ExerciseId}/training-max",
            new SetTrainingMaxRequest(null, 100m, 5, null));
        tm.StatusCode.Should().Be(HttpStatusCode.OK);
        (await tm.Content.ReadFromJsonAsync<SetTrainingMaxResult>())!.TrainingMax.Should().Be(105m);

        var withTm = (await client.GetFromJsonAsync<WeekPlanDto>($"/api/v1/workouts/{workoutId}/weeks/1/days/2/plan"))!;
        withTm.Exercises.Single(e => e.ExerciseId == pause.ExerciseId).PlannedSets.Should().OnlyContain(s => s.WeightKg > 0m);
        var finished = await client.PostAsJsonAsync("/api/v1/gym-sessions", Finish(withTm), Json);
        finished.StatusCode.Should().Be(HttpStatusCode.OK, await finished.Content.ReadAsStringAsync());
        (await finished.Content.ReadFromJsonAsync<FinishGymSessionResult>(Json))!.FinisherCompletionId.Should().NotBeNull();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<A2SDbContext>();
        var saved = await db.TrainingPlans.AsNoTracking().Include(p => p.Completions).SingleAsync(p => p.Id == new TrainingPlanId(applied.PlanId));
        var coolDown = saved.Completions.Single(c => c.Kind == SessionKind.Cardio);
        coolDown.Cardio!.DurationSeconds.Should().Be(600);
        coolDown.CardioTrackId.Should().BeNull();
    }

    [Fact]
    public async Task Applying_over_an_existing_plan_needs_confirmation_and_keeps_the_old_plan_and_its_history()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var old = (await (await client.PostAsJsonAsync("/api/v1/plans/suggested/cut-to-85", new StartSuggestedPlanRequest(100m, 140m, 60m, 180m, 95m)))
            .Content.ReadFromJsonAsync<StartSuggestedPlanResult>())!;
        var done = await client.PostAsJsonAsync($"/api/v1/plans/{old.PlanId}/completions",
            new CompleteSessionRequest(old.StartDate, SessionKind.Lift, LiftDay: DayNumber.Day1));
        done.EnsureSuccessStatusCode();

        var refused = await client.PostAsJsonAsync("/api/v1/plans/templates/plan-v3/apply", PlanV3());
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Cut to 85");
        (await client.GetFromJsonAsync<TrainingPlanDto>("/api/v1/plans/active"))!.Id.Should().Be(old.PlanId);

        var confirmed = await client.PostAsJsonAsync("/api/v1/plans/templates/plan-v3/apply", PlanV3(confirm: true));
        confirmed.StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.GetFromJsonAsync<TrainingPlanDto>("/api/v1/plans/active"))!.Name.Should().Be("Plan v3 — Base/Build/Transition");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<A2SDbContext>();
        var kept = await db.TrainingPlans.AsNoTracking().Include(p => p.Completions).SingleAsync(p => p.Id == new TrainingPlanId(old.PlanId));
        kept.IsActive.Should().BeFalse();
        kept.Completions.Should().ContainSingle();
    }
}
