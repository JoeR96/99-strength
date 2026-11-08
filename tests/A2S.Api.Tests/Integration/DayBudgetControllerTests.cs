using System.Net;
using System.Net.Http.Json;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.DTOs;
using A2S.Domain.Enums;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// GET /api/v1/workouts/{id}/days/{day}/budget on a four-day template workout.
/// Depends on the seeded, muscle-group-backfilled library (startup) and on the
/// AddSecondaryMuscleGroups migration being present.
/// </summary>
[Collection("Integration")]
public class DayBudgetControllerTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public DayBudgetControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDayBudget_ForDayOne_ReturnsSetsMapAndTimeEstimate()
    {
        var client = _factory.CreateAuthenticatedClient();
        var workoutId = await CreateFourDayWorkoutAsync(client);

        var response = await client.GetAsync($"/api/v1/workouts/{workoutId}/days/1/budget");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var budget = await response.Content.ReadFromJsonAsync<DayBudgetDto>();
        budget.Should().NotBeNull();
        budget!.HardSetsThisWeek.Should().ContainKey("Chest").WhoseValue.Should().Be(5, "bench press is a 5-set main lift in week 1");
        budget.HardSetsThisWeek.Should().ContainKey("Back");
        budget.Time.WarmUpMinutes.Should().Be(10);
        budget.Time.TotalMinutes.Should().BeGreaterThan(10);
        budget.Time.PerExercise.Select(e => e.Name).Should().Equal("Bench Press (Barbell)", "Lat Pulldown (Cable)");
        budget.Time.BudgetMinutes.Should().Be(55);
    }

    [Fact]
    public async Task GetDayBudget_WithExplicitWeek_UsesThatWeek()
    {
        var client = _factory.CreateAuthenticatedClient();
        var workoutId = await CreateFourDayWorkoutAsync(client);

        var response = await client.GetAsync($"/api/v1/workouts/{workoutId}/days/1/budget?week=7");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var budget = await response.Content.ReadFromJsonAsync<DayBudgetDto>();
        budget!.HardSetsThisWeek["Chest"].Should().Be(4, "week 7 is a deload");
    }

    [Fact]
    public async Task GetDayBudget_DayOutOfRange_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient();
        var workoutId = await CreateFourDayWorkoutAsync(client);

        var response = await client.GetAsync($"/api/v1/workouts/{workoutId}/days/7/budget");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetDayBudget_UnknownWorkout_Returns404()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"/api/v1/workouts/{Guid.Parse("0b0d6e70-0000-4000-8000-000000000001")}/days/1/budget");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDayBudget_AnotherUsersWorkout_Returns403()
    {
        var owner = _factory.CreateAuthenticatedClient("user_budget_owner");
        var workoutId = await CreateFourDayWorkoutAsync(owner);
        var stranger = _factory.CreateAuthenticatedClient("user_budget_stranger");

        var response = await stranger.GetAsync($"/api/v1/workouts/{workoutId}/days/1/budget");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<Guid> CreateFourDayWorkoutAsync(HttpClient client)
    {
        var exercises = new List<CreateExerciseRequest>
        {
            new()
            {
                TemplateName = "Bench Press (Barbell)",
                ExternalTemplateId = "test-bench-budget",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 1,
                TrainingMaxValue = 100m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Lat Pulldown (Cable)",
                ExternalTemplateId = "test-lat-pulldown-budget",
                Category = ExerciseCategory.Accessory,
                ProgressionType = "RepsPerSet",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 2,
                StartingWeight = 40m,
                WeightUnit = WeightUnit.Kilograms,
                StartingSets = 3,
                TargetSets = 5
            },
            new()
            {
                TemplateName = "Squat (Barbell)",
                ExternalTemplateId = "test-squat-budget",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day2,
                OrderInDay = 1,
                TrainingMaxValue = 120m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Overhead Press (Barbell)",
                ExternalTemplateId = "test-ohp-budget",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day3,
                OrderInDay = 1,
                TrainingMaxValue = 60m,
                TrainingMaxUnit = WeightUnit.Kilograms
            },
            new()
            {
                TemplateName = "Deadlift (Barbell)",
                ExternalTemplateId = "test-deadlift-budget",
                Category = ExerciseCategory.MainLift,
                ProgressionType = "Linear",
                AssignedDay = DayNumber.Day4,
                OrderInDay = 1,
                TrainingMaxValue = 140m,
                TrainingMaxUnit = WeightUnit.Kilograms
            }
        };

        var command = new CreateWorkoutCommand("Budget Test", ProgramVariant.FourDay, 21, exercises);
        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.Content.ReadFromJsonAsync<CreateWorkoutResponse>();
        return created!.Id;
    }
}
