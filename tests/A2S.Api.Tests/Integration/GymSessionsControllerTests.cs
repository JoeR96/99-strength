using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using A2S.Application.Commands.CompleteDay;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Commands.GymSessions;
using A2S.Application.Commands.Plans;
using A2S.Application.DTOs;
using A2S.Application.Queries.GetWeekPlan;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Services;
using A2S.Infrastructure.Persistence;
using A2S.Tests.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// Gym mode over HTTP on a real database: finishing completes the A2S2 day and the plan's lift slot in one
/// go (Today shows it Done), a replay of the same client session id changes nothing, the week plan carries
/// rest seconds, and a lift slot keeps its warm-up and finisher. Test users have no Hevy key, so the post
/// is Skipped; the Hevy path itself is covered in A2S.Application.Tests (GymSessionTests).
/// </summary>
[Collection("Integration")]
public class GymSessionsControllerTests
{
    private static readonly DateOnly Wed2Jan2030 = new(2030, 1, 2);
    private static readonly DateOnly Mon7Jan2030 = new(2030, 1, 7);
    private static readonly DateTimeOffset Started = new(2030, 1, 7, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Ended = new(2030, 1, 7, 19, 2, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly TestWebApplicationFactory<Program> _factory;

    public GymSessionsControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static async Task<Guid> CreateWorkoutAsync(HttpClient client)
    {
        var command = new CreateWorkoutCommand(
            Name: "Gym Mode Test",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21,
            Exercises:
            [
                new CreateExerciseRequest
                {
                    TemplateName = "Bench Press (Barbell)",
                    ExternalTemplateId = $"bench-gym-{Guid.NewGuid():N}",
                    Category = ExerciseCategory.MainLift,
                    ProgressionType = "Linear",
                    AssignedDay = DayNumber.Day1,
                    OrderInDay = 1,
                    TrainingMaxValue = 100m,
                    TrainingMaxUnit = WeightUnit.Kilograms
                }
            ]);
        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>())!.Id;
    }

    private static async Task<Guid> CreatePlanAsync(HttpClient client, Guid workoutId, Guid? finisherProtocol = null)
    {
        var body = new CreateTrainingPlanCommand(
            "Gym plan",
            Wed2Jan2030,
            [
                new ScheduledSessionDto(DayOfWeek.Monday, SessionKind.Lift, DayNumber.Day1, null, null, "Upper A", null, finisherProtocol),
                new ScheduledSessionDto(DayOfWeek.Sunday, SessionKind.Rest, null, null, null, "Rest")
            ],
            95m,
            LinkedWorkoutId: workoutId);
        var created = await client.PostAsJsonAsync("/api/v1/plans", body);
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<TrainingPlanDto>())!.Id;
    }

    private static async Task<FinishGymSessionCommand> FinishBodyAsync(HttpClient client, Guid workoutId, Guid clientSession)
    {
        var plan = (await client.GetFromJsonAsync<WeekPlanDto>($"/api/v1/workouts/{workoutId}/weeks/1/days/1/plan"))!;
        var bench = plan.Exercises.Single();
        var performances = new List<ExercisePerformanceRequest>
        {
            new()
            {
                ExerciseId = bench.ExerciseId,
                CompletedSets = bench.PlannedSets.Select(s => new CompletedSetRequest
                {
                    SetNumber = s.SetNumber,
                    Weight = s.WeightKg,
                    WeightUnit = WeightUnit.Kilograms,
                    ActualReps = s.IsAmrap ? s.TargetReps + 5 : s.TargetReps,
                    WasAmrap = s.IsAmrap
                }).ToList()
            }
        };
        return new FinishGymSessionCommand(clientSession, workoutId, DayNumber.Day1, Started, Ended, performances, [true]);
    }

    [Fact]
    public async Task Finish_FromASecondDevice_ForTheSameDay_Answers409_AndProgressesOnce()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var workoutId = await CreateWorkoutAsync(client);
        await CreatePlanAsync(client, workoutId);
        var phone = await FinishBodyAsync(client, workoutId, Guid.NewGuid());
        var tablet = phone with { ClientSessionId = Guid.NewGuid() };

        var first = await client.PostAsJsonAsync("/api/v1/gym-sessions", phone, Json);
        var second = await client.PostAsJsonAsync("/api/v1/gym-sessions", tablet, Json);
        var replay = await client.PostAsJsonAsync("/api/v1/gym-sessions", phone, Json);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await second.Content.ReadAsStringAsync()).Should().Contain("This day was already finished on another device");
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replay.Content.ReadFromJsonAsync<FinishGymSessionResult>(Json))!.Replayed.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<A2SDbContext>();
        var workout = await db.Workouts.AsNoTracking().SingleAsync(w => w.Id == new WorkoutId(workoutId));
        workout.CompletedActivities.Should().ContainSingle();
        (await db.HevyWorkoutPosts.AsNoTracking().CountAsync(p => p.ClientSessionId == tablet.ClientSessionId)).Should().Be(0);
    }

    [Fact]
    public async Task Finish_CompletesTheDayAndTheSlot_AndAReplayChangesNothing()
    {
        var userId = $"user_{Guid.NewGuid():N}";
        var client = _factory.CreateAuthenticatedClient(userId);
        var workoutId = await CreateWorkoutAsync(client);
        await CreatePlanAsync(client, workoutId);
        var clientSession = Guid.NewGuid();
        var body = await FinishBodyAsync(client, workoutId, clientSession);

        var first = await client.PostAsJsonAsync("/api/v1/gym-sessions", body, Json);
        var second = await client.PostAsJsonAsync("/api/v1/gym-sessions", body, Json);

        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstResult = (await first.Content.ReadFromJsonAsync<FinishGymSessionResult>(Json))!;
        var secondResult = (await second.Content.ReadFromJsonAsync<FinishGymSessionResult>(Json))!;
        firstResult.HevyStatus.Should().Be("Skipped", "test users have no Hevy key");
        firstResult.Replayed.Should().BeFalse();
        firstResult.CompleteDay!.ProgressionChanges.Should().ContainSingle();
        secondResult.Replayed.Should().BeTrue();
        secondResult.CompletionId.Should().Be(firstResult.CompletionId);

        var today = await client.GetFromJsonAsync<TodayDto>($"/api/v1/plans/today?date={Mon7Jan2030:yyyy-MM-dd}", Json);
        var lift = today!.Sessions.Single();
        lift.Status.Should().Be(SessionStatus.Done);
        lift.GymSessionId.Should().Be(clientSession);
        lift.HevyPostStatus.Should().Be("Skipped");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<A2SDbContext>();
        var workout = await db.Workouts.AsNoTracking().SingleAsync(w => w.Id == new WorkoutId(workoutId));
        workout.CompletedActivities.Should().ContainSingle();
        var post = await db.HevyWorkoutPosts.AsNoTracking().SingleAsync(p => p.UserId == new UserId(userId));
        post.ClientSessionId.Should().Be(clientSession);
        post.Status.Should().Be(HevyPostStatus.Skipped);
        post.StartedAtUtc.Should().Be(Started);
    }

    [Fact]
    public async Task Finish_ForSomeoneElsesWorkout_IsNotFound()
    {
        var owner = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var workoutId = await CreateWorkoutAsync(owner);
        var body = await FinishBodyAsync(owner, workoutId, Guid.NewGuid());
        var other = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await other.PostAsJsonAsync("/api/v1/gym-sessions", body, Json);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RetryHevy_ForAnUnknownSession_IsNotFound()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await client.PostAsync($"/api/v1/gym-sessions/{Guid.NewGuid()}/hevy-retry", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WeekPlan_CarriesRestSeconds_AndTheScheduleKeepsWarmUpAndFinisher()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var workoutId = await CreateWorkoutAsync(client);
        var finisher = Guid.Parse("c4c4c4c4-0000-0000-0000-00000000aaaa");
        await CreatePlanAsync(client, workoutId, finisher);

        var plan = await client.GetFromJsonAsync<WeekPlanDto>($"/api/v1/workouts/{workoutId}/weeks/1/days/1/plan");
        var active = await client.GetFromJsonAsync<TrainingPlanDto>("/api/v1/plans/active", Json);

        plan!.Exercises.Single().RestSeconds.Should().Be(180);
        active!.Schedule.Single(s => s.Kind == SessionKind.Lift).FinisherCardioProtocolId.Should().Be(finisher);
        active.Schedule.Single(s => s.Kind == SessionKind.Lift).WarmUpRoutineId.Should().BeNull();
    }
}
