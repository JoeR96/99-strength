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
        preview.Slots.Should().HaveCount(11);
        preview.Routines.Should().HaveCount(5);
        preview.CardioTracks.Select(t => t.Key).Should().Equal("walk-to-run");
        preview.ToFillIn.Should().Contain(i => i.Contains("Front Squat")).And.NotContain(i => i.Contains("Pause Squat (Barbell)"));
        preview.Inputs.Where(i => i.Key.EndsWith("Tm")).Should().OnlyContain(i => i.Optional);
        (await client.GetAsync("/api/v1/plans/templates/nope")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Apply_with_pending_tms_then_set_a_tm_and_finish_lower_b_leaving_the_pending_walk_out()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.PostAsJsonAsync("/api/v1/plans/templates/plan-v3/apply", PlanV3());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var applied = (await response.Content.ReadFromJsonAsync<StartSuggestedPlanResult>())!;
        var again = await client.PostAsJsonAsync("/api/v1/plans/templates/plan-v3/apply", PlanV3());
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        (await again.Content.ReadFromJsonAsync<StartSuggestedPlanResult>())!.AlreadyStarted.Should().BeTrue();

        var plan = (await client.GetFromJsonAsync<TrainingPlanDto>("/api/v1/plans/active"))!;
        var routines = (await client.GetFromJsonAsync<List<MobilityRoutineDto>>("/api/v1/mobility-routines"))!;
        routines.Single(r => r.Name == "Full-body flow").Kind.Should().Be(A2S.Domain.Aggregates.Mobility.MobilityRoutineKind.YogaFlow);
        routines.Single(r => r.Name == "Warm-up (upper)").Kind.Should().Be(A2S.Domain.Aggregates.Mobility.MobilityRoutineKind.WarmUp);
        var ladder = routines.Single(r => r.Name == "Upper back & chest").Drills.Single(d => d.Name == "Reverse plank ladder");
        ladder.Levels!.Should().HaveCount(3);
        ladder.CurrentLevel.Should().Be(0);
        routines.Single(r => r.Name == "Hips & lower back").Drills.Single(d => d.Name == "Side plank").Levels![1].Range.Should().Be(new DoseRangeDto(20, 30, 5));
        plan.Name.Should().Be("Falador Fat Furnace");
        plan.Schedule.Where(s => s.LiftDay != null).Should().HaveCount(4)
            .And.OnlyContain(s => s.FinisherFixed == null && s.FinisherCardioTrackId == null && s.FinisherCardioProtocolId == null);

        var workoutId = applied.WorkoutId!.Value;
        var lowerA = (await client.GetFromJsonAsync<WeekPlanDto>($"/api/v1/workouts/{workoutId}/weeks/1/days/2/plan"))!;
        var pauseSquat = lowerA.Exercises.Single(e => e.Name == "Pause Squat (Barbell)");
        pauseSquat.Metadata.IsTrainingMaxPending.Should().BeFalse();
        pauseSquat.PlannedSets.Should().HaveCount(4);
        lowerA.Exercises.Should().OnlyContain(e => e.SupersetGroup == null);

        var lowerB = (await client.GetFromJsonAsync<WeekPlanDto>($"/api/v1/workouts/{workoutId}/weeks/1/days/4/plan"))!;
        var front = lowerB.Exercises.Single(e => e.Name == "Front Squat");
        front.Metadata.IsTrainingMaxPending.Should().BeTrue();
        front.PlannedSets.Should().OnlyContain(s => s.WeightKg == 0m);
        lowerB.Exercises[^1].Name.Should().Be("Treadmill");
        lowerB.Exercises[^1].CardioBlock.Should().NotBeNull();

        var friday = applied.StartDate.AddDays(4);
        var started = new DateTimeOffset(friday.ToDateTime(new TimeOnly(18, 0)), TimeSpan.Zero);
        FinishGymSessionCommand Finish(WeekPlanDto day) => new(Guid.NewGuid(), workoutId, DayNumber.Day4, started, started.AddMinutes(60),
            [
                new ExercisePerformanceRequest
                {
                    ExerciseId = front.ExerciseId,
                    CompletedSets = day.Exercises.Single(e => e.ExerciseId == front.ExerciseId).PlannedSets.Select(s => new CompletedSetRequest
                    {
                        SetNumber = s.SetNumber, Weight = 80m, WeightUnit = WeightUnit.Kilograms, ActualReps = s.TargetReps, WasAmrap = s.IsAmrap
                    }).ToList()
                }
            ]);

        var blocked = await client.PostAsJsonAsync("/api/v1/gym-sessions", Finish(lowerB), Json);
        blocked.IsSuccessStatusCode.Should().BeFalse();
        (await blocked.Content.ReadAsStringAsync()).Should().Contain("Set your training max for Front Squat");

        var tm = await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/exercises/{front.ExerciseId}/training-max",
            new SetTrainingMaxRequest(null, 100m, 5, null));
        tm.StatusCode.Should().Be(HttpStatusCode.OK);
        (await tm.Content.ReadFromJsonAsync<SetTrainingMaxResult>())!.TrainingMax.Should().Be(105m);

        var withTm = (await client.GetFromJsonAsync<WeekPlanDto>($"/api/v1/workouts/{workoutId}/weeks/1/days/4/plan"))!;
        withTm.Exercises.Single(e => e.ExerciseId == front.ExerciseId).PlannedSets.Should().OnlyContain(s => s.WeightKg > 0m);
        var finished = await client.PostAsJsonAsync("/api/v1/gym-sessions", Finish(withTm), Json);
        finished.StatusCode.Should().Be(HttpStatusCode.OK, await finished.Content.ReadAsStringAsync());
        (await finished.Content.ReadFromJsonAsync<FinishGymSessionResult>(Json))!.FinisherCompletionId.Should().BeNull();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<A2SDbContext>();
        var saved = await db.TrainingPlans.AsNoTracking().Include(p => p.Completions).SingleAsync(p => p.Id == new TrainingPlanId(applied.PlanId));
        saved.Completions.Should().ContainSingle(c => c.Kind == SessionKind.Lift).And.NotContain(c => c.Kind == SessionKind.Cardio);
    }

    [Fact]
    public async Task Applying_the_active_template_again_with_confirm_replace_starts_a_fresh_copy()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var first = (await (await client.PostAsJsonAsync("/api/v1/plans/templates/plan-v3/apply", PlanV3())).Content.ReadFromJsonAsync<StartSuggestedPlanResult>())!;

        var fresh = await client.PostAsJsonAsync("/api/v1/plans/templates/plan-v3/apply", PlanV3(confirm: true));

        fresh.StatusCode.Should().Be(HttpStatusCode.Created);
        var second = (await fresh.Content.ReadFromJsonAsync<StartSuggestedPlanResult>())!;
        second.AlreadyStarted.Should().BeFalse();
        second.PlanId.Should().NotBe(first.PlanId);
        second.WorkoutId.Should().NotBe(first.WorkoutId!.Value);
        (await client.GetFromJsonAsync<TrainingPlanDto>("/api/v1/plans/active"))!.Id.Should().Be(second.PlanId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<A2SDbContext>();
        (await db.TrainingPlans.AsNoTracking().SingleAsync(p => p.Id == new TrainingPlanId(first.PlanId))).IsActive.Should().BeFalse();
        (await db.Workouts.AsNoTracking().SingleAsync(w => w.Id == new WorkoutId(first.WorkoutId.Value)))
            .Status.Should().Be(WorkoutStatus.Paused);
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
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Lumbridge Lard Shed");
        (await client.GetFromJsonAsync<TrainingPlanDto>("/api/v1/plans/active"))!.Id.Should().Be(old.PlanId);

        var confirmed = await client.PostAsJsonAsync("/api/v1/plans/templates/plan-v3/apply", PlanV3(confirm: true));
        confirmed.StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.GetFromJsonAsync<TrainingPlanDto>("/api/v1/plans/active"))!.Name.Should().Be("Falador Fat Furnace");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<A2SDbContext>();
        var kept = await db.TrainingPlans.AsNoTracking().Include(p => p.Completions).SingleAsync(p => p.Id == new TrainingPlanId(old.PlanId));
        kept.IsActive.Should().BeFalse();
        kept.Completions.Should().ContainSingle();
    }
}
