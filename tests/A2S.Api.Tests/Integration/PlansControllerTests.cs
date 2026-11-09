using System.Net;
using System.Net.Http.Json;
using A2S.Api.Contracts.Requests;
using A2S.Application.Commands.Cardio;
using A2S.Application.Commands.Mobility;
using A2S.Application.Commands.Plans;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Enums;
using A2S.Domain.Services;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// The plan API end to end: create → today → complete → today shows Done; a plan is invisible to
/// other users; anonymous calls are refused. Needs the TrainingPlans/SessionCompletions/CardioProtocols/
/// MobilityRoutines migration the integrator generates.
/// </summary>
[Collection("Integration")]
public class PlansControllerTests
{
    // Far enough ahead that these dates stay "planned" whenever the suite runs.
    private static readonly DateOnly Wed2Jan2030 = new(2030, 1, 2);
    private static readonly DateOnly Mon7Jan2030 = new(2030, 1, 7);
    private static readonly DateOnly Wed9Jan2030 = new(2030, 1, 9);

    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly HttpClient _anonymous;

    public PlansControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _anonymous = factory.CreateClient();
    }

    private static CreateTrainingPlanCommand CreatePlanBody() => new(
        "Cut to 85 — Nov 2026",
        Wed2Jan2030,
        [
            new ScheduledSessionDto(DayOfWeek.Monday, SessionKind.Lift, DayNumber.Day1, null, null, "Upper A"),
            new ScheduledSessionDto(DayOfWeek.Tuesday, SessionKind.Lift, DayNumber.Day2, null, null, "Lower A"),
            new ScheduledSessionDto(DayOfWeek.Wednesday, SessionKind.Cardio, null, null, null, "Norwegian 4×4"),
            new ScheduledSessionDto(DayOfWeek.Wednesday, SessionKind.Mobility, null, null, null, "Mobility B"),
            new ScheduledSessionDto(DayOfWeek.Sunday, SessionKind.Rest, null, null, null, "Rest")
        ],
        95m);

    private async Task<TrainingPlanDto> CreatePlanAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/plans", CreatePlanBody());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<TrainingPlanDto>())!;
    }

    [Fact]
    public async Task Create_SnapsStartToTheComingMondayAndBecomesActive()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var plan = await CreatePlanAsync(client);
        var active = await client.GetFromJsonAsync<TrainingPlanDto>("/api/v1/plans/active");

        plan.StartDate.Should().Be(Mon7Jan2030);
        plan.IsActive.Should().BeTrue();
        plan.Schedule.Should().HaveCount(5);
        active!.Id.Should().Be(plan.Id);
    }

    [Fact]
    public async Task CreateThenToday_CompleteThenTodayShowsDone()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var plan = await CreatePlanAsync(client);

        var before = await client.GetFromJsonAsync<TodayDto>($"/api/v1/plans/today?date={Mon7Jan2030:yyyy-MM-dd}");
        var complete = await client.PostAsJsonAsync($"/api/v1/plans/{plan.Id}/completions",
            new CompleteSessionRequest(Mon7Jan2030, SessionKind.Lift, LiftDay: DayNumber.Day1));
        var after = await client.GetFromJsonAsync<TodayDto>($"/api/v1/plans/today?date={Mon7Jan2030:yyyy-MM-dd}");

        before!.Date.Should().Be(Mon7Jan2030);
        before.Sessions.Should().ContainSingle().Which.Status.Should().Be(SessionStatus.Planned);
        before.Week.LiftsPlanned.Should().Be(2);
        before.Week.LiftsDone.Should().Be(0);

        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        var completion = await complete.Content.ReadFromJsonAsync<SessionCompletionDto>();
        completion!.Source.Should().Be(CompletionSource.Manual);

        after!.Sessions.Should().ContainSingle().Which.Status.Should().Be(SessionStatus.Done);
        after.Sessions[0].CompletionId.Should().Be(completion.Id);
        after.Week.LiftsDone.Should().Be(1);
    }

    [Fact]
    public async Task CompleteTwiceWithTheSameHevyId_ReturnsTheSameCompletion()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var plan = await CreatePlanAsync(client);
        var body = new CompleteSessionRequest(Mon7Jan2030, SessionKind.Lift, LiftDay: DayNumber.Day1, HevyWorkoutId: "hevy-abc");

        var first = await (await client.PostAsJsonAsync($"/api/v1/plans/{plan.Id}/completions", body)).Content.ReadFromJsonAsync<SessionCompletionDto>();
        var second = await (await client.PostAsJsonAsync($"/api/v1/plans/{plan.Id}/completions", body)).Content.ReadFromJsonAsync<SessionCompletionDto>();
        var active = await client.GetFromJsonAsync<TrainingPlanDto>("/api/v1/plans/active");

        second!.Id.Should().Be(first!.Id);
        active!.Completions.Should().ContainSingle();
    }

    [Fact]
    public async Task WednesdayToday_ReturnsBothSessions_AndUndoClearsADoneOne()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var plan = await CreatePlanAsync(client);
        var completion = await (await client.PostAsJsonAsync($"/api/v1/plans/{plan.Id}/completions",
            new CompleteSessionRequest(Wed9Jan2030, SessionKind.Cardio, Cardio: new CardioLogDto(2580, 160, 8, null, "Hills"))))
            .Content.ReadFromJsonAsync<SessionCompletionDto>();

        var today = await client.GetFromJsonAsync<TodayDto>($"/api/v1/plans/today?date={Wed9Jan2030:yyyy-MM-dd}");
        var undo = await client.DeleteAsync($"/api/v1/plans/{plan.Id}/completions/{completion!.Id}");
        var afterUndo = await client.GetFromJsonAsync<TodayDto>($"/api/v1/plans/today?date={Wed9Jan2030:yyyy-MM-dd}");

        today!.Sessions.Select(s => s.Status).Should().Equal(SessionStatus.Done, SessionStatus.Planned);
        undo.StatusCode.Should().Be(HttpStatusCode.NoContent);
        afterUndo!.Sessions.Select(s => s.Status).Should().Equal(SessionStatus.Planned, SessionStatus.Planned);
    }

    [Fact]
    public async Task PausePhase_MarksSessionsPaused_AndWeeklyGoalsExcludeThem()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var plan = await CreatePlanAsync(client);

        var phase = await client.PostAsJsonAsync($"/api/v1/plans/{plan.Id}/phases",
            new AddPhaseRequest(PhaseType.Pause, Wed9Jan2030, new DateOnly(2030, 1, 13), "Away"));
        var today = await client.GetFromJsonAsync<TodayDto>($"/api/v1/plans/today?date={Wed9Jan2030:yyyy-MM-dd}");
        var goals = await client.GetFromJsonAsync<WeeklyGoalsDto>($"/api/v1/plans/weekly-goals?date={Wed9Jan2030:yyyy-MM-dd}");
        var calendar = await client.GetFromJsonAsync<List<PlanDayDto>>("/api/v1/plans/calendar?year=2030&month=1");

        phase.StatusCode.Should().Be(HttpStatusCode.OK);
        today!.Phase.Should().Be(PhaseType.Pause);
        today.Sessions.Should().AllSatisfy(s => s.Status.Should().Be(SessionStatus.Paused));
        goals!.WeekStart.Should().Be(Mon7Jan2030);
        goals.LiftsPlanned.Should().Be(2);
        goals.CardioPlanned.Should().Be(0);
        goals.MobilityPlanned.Should().Be(0);
        calendar.Should().HaveCount(31);
    }

    [Fact]
    public async Task CheckpointsAndAllowances_RoundTrip()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var plan = await CreatePlanAsync(client);

        var checkpoint = await client.PostAsJsonAsync($"/api/v1/plans/{plan.Id}/checkpoints", new AddCheckpointRequest(new DateOnly(2030, 2, 4), 92m, "4 weeks"));
        var allowance = await client.PostAsJsonAsync($"/api/v1/plans/{plan.Id}/allowances", new UseAllowanceRequest(Wed9Jan2030, AllowanceKind.FlexibleMeal));
        var goals = await client.GetFromJsonAsync<WeeklyGoalsDto>($"/api/v1/plans/weekly-goals?date={Wed9Jan2030:yyyy-MM-dd}");
        var removeAllowance = await client.DeleteAsync($"/api/v1/plans/{plan.Id}/allowances?date={Wed9Jan2030:yyyy-MM-dd}&kind={(int)AllowanceKind.FlexibleMeal}");
        var removeCheckpoint = await client.DeleteAsync($"/api/v1/plans/{plan.Id}/checkpoints/2030-02-04");
        var active = await client.GetFromJsonAsync<TrainingPlanDto>("/api/v1/plans/active");

        checkpoint.StatusCode.Should().Be(HttpStatusCode.OK);
        (await checkpoint.Content.ReadFromJsonAsync<TrainingPlanDto>())!.Checkpoints.Should().ContainSingle().Which.TargetKg.Should().Be(92m);
        allowance.StatusCode.Should().Be(HttpStatusCode.OK);
        goals!.FlexibleMealsUsed.Should().Be(1);
        goals.FlexibleMealsAllowed.Should().Be(1);
        removeAllowance.StatusCode.Should().Be(HttpStatusCode.NoContent);
        removeCheckpoint.StatusCode.Should().Be(HttpStatusCode.NoContent);
        active!.Checkpoints.Should().BeEmpty();
        active.AllowanceUses.Should().BeEmpty();
    }

    [Fact]
    public async Task OtherUsersPlan_IsNotFound()
    {
        var owner = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var plan = await CreatePlanAsync(owner);
        var intruder = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var complete = await intruder.PostAsJsonAsync($"/api/v1/plans/{plan.Id}/completions",
            new CompleteSessionRequest(Mon7Jan2030, SessionKind.Lift, LiftDay: DayNumber.Day1));
        var active = await intruder.GetAsync("/api/v1/plans/active");
        var today = await intruder.GetAsync("/api/v1/plans/today");

        complete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        active.StatusCode.Should().Be(HttpStatusCode.NotFound);
        today.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WithoutAuth_IsUnauthorized()
    {
        var active = await _anonymous.GetAsync("/api/v1/plans/active");
        var create = await _anonymous.PostAsJsonAsync("/api/v1/plans", CreatePlanBody());
        var cardio = await _anonymous.GetAsync("/api/v1/cardio-protocols");
        var mobility = await _anonymous.GetAsync("/api/v1/mobility-routines");

        active.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        create.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        cardio.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        mobility.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CardioProtocols_CreateAdvanceAndList()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var created = await client.PostAsJsonAsync("/api/v1/cardio-protocols", new CreateCardioProtocolCommand(CardioProtocolKind.CouchToFiveK, WeekIndex: 1));
        var protocol = await created.Content.ReadFromJsonAsync<CardioProtocolDto>();
        var repeat = await client.PutAsJsonAsync($"/api/v1/cardio-protocols/{protocol!.Id}/repeat-week", new SetRepeatWeekRequest(true));
        var advanced = await (await client.PostAsync($"/api/v1/cardio-protocols/{protocol.Id}/advance", null)).Content.ReadFromJsonAsync<CardioProtocolDto>();
        var badLiss = await client.PostAsJsonAsync("/api/v1/cardio-protocols", new CreateCardioProtocolCommand(CardioProtocolKind.Liss, Minutes: 45));
        var list = await client.GetFromJsonAsync<List<CardioProtocolDto>>("/api/v1/cardio-protocols");

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        protocol.Intervals.Should().HaveCount(18);
        repeat.StatusCode.Should().Be(HttpStatusCode.OK);
        advanced!.WeekIndex.Should().Be(1);
        advanced.RepeatWeek.Should().BeFalse();
        badLiss.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        list.Should().ContainSingle().Which.Id.Should().Be(protocol.Id);
    }

    [Fact]
    public async Task MobilityRoutines_CreateReplaceDrillsAndList()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var drills = new List<MobilityDrillDto>
        {
            new("90/90 breathing", 2, new DrillDoseDto(DoseUnit.Breaths, 5), SideBias.None, null, "Back-to-wall breathing", false),
            new("Open books", 2, new DrillDoseDto(DoseUnit.Reps, 5), SideBias.Left, null, "Side-on wall rotation", true)
        };

        var created = await client.PostAsJsonAsync("/api/v1/mobility-routines", new CreateMobilityRoutineCommand("Mobility A", 10, drills));
        var routine = await created.Content.ReadFromJsonAsync<MobilityRoutineDto>();
        var replaced = await client.PutAsJsonAsync($"/api/v1/mobility-routines/{routine!.Id}/drills",
            new ReplaceDrillsRequest([new MobilityDrillDto("Bird dog", 3, new DrillDoseDto(DoseUnit.Reps, 5), SideBias.Right, null, null, true)]));
        var list = await client.GetFromJsonAsync<List<MobilityRoutineDto>>("/api/v1/mobility-routines");

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        routine.Drills.Should().HaveCount(2);
        replaced.StatusCode.Should().Be(HttpStatusCode.OK);
        list.Should().ContainSingle().Which.Drills.Should().ContainSingle().Which.Name.Should().Be("Bird dog");
    }
}
