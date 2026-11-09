using System.Net.Http.Json;
using A2S.Application.Commands.Plans;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Infrastructure.Persistence;
using A2S.Tests.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// <c>GET /plans/today</c> and <c>/plans/calendar</c> carry each session's pushed Hevy routine id.
/// The test host has no Hevy stub, so the link a push would leave is written to HevyLinks directly
/// with the pusher's own key scheme; without one the field is null.
/// </summary>
[Collection("Integration")]
public class HevyRoutineLinkOnTodayTests
{
    private static readonly DateOnly Wed2Jan2030 = new(2030, 1, 2);
    private static readonly DateOnly Mon7Jan2030 = new(2030, 1, 7);
    private static readonly DateOnly Wed9Jan2030 = new(2030, 1, 9);

    private readonly TestWebApplicationFactory<Program> _factory;

    public HevyRoutineLinkOnTodayTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static CreateTrainingPlanCommand CreatePlanBody() => new(
        "Cut to 85 — Nov 2026",
        Wed2Jan2030,
        [
            new ScheduledSessionDto(DayOfWeek.Monday, SessionKind.Lift, DayNumber.Day1, null, null, "Upper A"),
            new ScheduledSessionDto(DayOfWeek.Wednesday, SessionKind.Cardio, null, null, null, "Norwegian 4×4"),
            new ScheduledSessionDto(DayOfWeek.Wednesday, SessionKind.Mobility, null, null, null, "Mobility B"),
            new ScheduledSessionDto(DayOfWeek.Sunday, SessionKind.Rest, null, null, null, "Rest")
        ],
        95m);

    [Fact]
    public async Task Today_WithoutAPush_HasNullHevyRoutineId()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        await client.PostAsJsonAsync("/api/v1/plans", CreatePlanBody());

        var today = await client.GetFromJsonAsync<TodayDto>($"/api/v1/plans/today?date={Mon7Jan2030:yyyy-MM-dd}");

        today!.Sessions.Should().ContainSingle();
        today.Sessions[0].HevyRoutineId.Should().BeNull();
    }

    [Fact]
    public async Task Today_AfterAPushLeftALink_CarriesTheHevyRoutineId()
    {
        var userId = $"user_{Guid.NewGuid():N}";
        var client = _factory.CreateAuthenticatedClient(userId);
        var created = await client.PostAsJsonAsync("/api/v1/plans", CreatePlanBody());
        var plan = (await created.Content.ReadFromJsonAsync<TrainingPlanDto>())!;
        var planId = new TrainingPlanId(plan.Id);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<A2SDbContext>();
            var owner = new UserId(userId);
            var now = DateTimeOffset.UtcNow;
            db.HevyLinks.Add(HevyLink.Create(owner, HevyLinkKind.Routine, HevyLocalKeys.LiftRoutine(planId, Mon7Jan2030, DayNumber.Day1), "rt_upper_a", now));
            db.HevyLinks.Add(HevyLink.Create(owner, HevyLinkKind.Routine, HevyLocalKeys.SlotRoutine(planId, SessionKind.Cardio, "Norwegian 4×4"), "rt_4x4", now));
            await db.SaveChangesAsync();
        }

        var monday = await client.GetFromJsonAsync<TodayDto>($"/api/v1/plans/today?date={Mon7Jan2030:yyyy-MM-dd}");
        var wednesday = await client.GetFromJsonAsync<TodayDto>($"/api/v1/plans/today?date={Wed9Jan2030:yyyy-MM-dd}");
        var calendar = await client.GetFromJsonAsync<List<PlanDayDto>>("/api/v1/plans/calendar?year=2030&month=1");

        monday!.Sessions.Single().HevyRoutineId.Should().Be("rt_upper_a");
        wednesday!.Sessions.Select(s => s.HevyRoutineId).Should().Equal("rt_4x4", null);
        calendar!.Single(d => d.Date == Mon7Jan2030).Sessions.Single().HevyRoutineId.Should().Be("rt_upper_a");
        calendar.Single(d => d.Date == Mon7Jan2030.AddDays(7)).Sessions.Single().HevyRoutineId.Should().BeNull("the next Monday was not pushed");
    }

    [Fact]
    public async Task Today_DoesNotShowAnotherUsersRoutineLink()
    {
        var owner = $"user_{Guid.NewGuid():N}";
        var client = _factory.CreateAuthenticatedClient(owner);
        var plan = (await (await client.PostAsJsonAsync("/api/v1/plans", CreatePlanBody())).Content.ReadFromJsonAsync<TrainingPlanDto>())!;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<A2SDbContext>();
            db.HevyLinks.Add(HevyLink.Create(new UserId($"user_{Guid.NewGuid():N}"), HevyLinkKind.Routine,
                HevyLocalKeys.LiftRoutine(new TrainingPlanId(plan.Id), Mon7Jan2030, DayNumber.Day1), "rt_someone_else", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        var today = await client.GetFromJsonAsync<TodayDto>($"/api/v1/plans/today?date={Mon7Jan2030:yyyy-MM-dd}");

        today!.Sessions.Single().HevyRoutineId.Should().BeNull();
    }
}
