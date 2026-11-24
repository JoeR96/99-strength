using System.Net;
using System.Net.Http.Json;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.Enums;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// Cardio as an exercise over HTTP: the incline walk asks for its incline and speed first (completing before that
/// is a 400, never a 500), then a finished Easy walk steps the lane up. The settings endpoint is Clerk only.
/// </summary>
[Collection("Integration")]
public class CardioExerciseIntegrationTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public CardioExerciseIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record CreatedWorkout(Guid Id);

    private static async Task<Guid> CreateWorkoutWithWalkAsync(HttpClient client)
    {
        CreateExerciseRequest Lift(string name, DayNumber day) => new()
        {
            TemplateName = name,
            ExternalTemplateId = $"test-{name}",
            Category = ExerciseCategory.Accessory,
            ProgressionType = "RepsPerSet",
            AssignedDay = day,
            OrderInDay = 1,
            StartingWeight = 20m,
            WeightUnit = WeightUnit.Kilograms,
            StartingSets = 3,
            TargetSets = 5
        };

        var exercises = new List<CreateExerciseRequest>
        {
            Lift("Triceps Pushdown", DayNumber.Day1),
            new()
            {
                TemplateName = "Treadmill",
                ExternalTemplateId = "243710DE",
                Category = ExerciseCategory.Cardio,
                ProgressionType = "CardioStage",
                AssignedDay = DayNumber.Day1,
                OrderInDay = 2,
                CardioLane = CardioLane.Easy
            },
            Lift("Bicep Curl (Cable)", DayNumber.Day2),
            Lift("Leg Extension (Machine)", DayNumber.Day3),
            Lift("Lateral Raise (Cable)", DayNumber.Day4)
        };

        var response = await client.PostAsJsonAsync("/api/v1/workouts",
            new CreateWorkoutCommand("Cardio Test", ProgramVariant.FourDay, 21, exercises));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CreatedWorkout>())!.Id;
    }

    private static async Task<ExerciseDto> WalkAsync(HttpClient client)
    {
        var workout = await client.GetFromJsonAsync<WorkoutDto>("/api/v1/workouts/current");
        return workout!.Exercises.Single(e => e.Category == ExerciseCategory.Cardio);
    }

    private static object EasyWalk(Guid exerciseId) => new
    {
        performances = new[]
        {
            new { exerciseId, cardio = new { finished = true, effort = "Easy", totalSeconds = 600 } }
        }
    };

    [Fact]
    public async Task Api_PutCardioSettings_ThenCompleteDay_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();
        var workoutId = await CreateWorkoutWithWalkAsync(client);
        var walk = await WalkAsync(client);

        var pending = await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/days/1/complete", EasyWalk(walk.Id));
        var settings = await client.PutAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/exercises/{walk.Id}/cardio-settings", new { intensity = 5m, speed = 5.5m });
        var updated = await settings.Content.ReadFromJsonAsync<ExerciseDto>();
        var complete = await client.PostAsJsonAsync($"/api/v1/workouts/{workoutId}/days/1/complete", EasyWalk(walk.Id));

        pending.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await pending.Content.ReadAsStringAsync()).Should().Contain("Set the Treadmill settings before finishing it.");
        settings.StatusCode.Should().Be(HttpStatusCode.OK);
        updated!.Progression.Should().BeOfType<CardioStageProgressionDto>()
            .Which.Prescription.Should().Be("10 min @ 5 % incline, 5.5 km/h");
        complete.StatusCode.Should().Be(HttpStatusCode.OK, await complete.Content.ReadAsStringAsync());
        (await WalkAsync(client)).Progression.Should().BeOfType<CardioStageProgressionDto>()
            .Which.Prescription.Should().Be("12 min @ 5 % incline, 5.5 km/h");
    }

    [Fact]
    public async Task Api_CardioSettings_WithPersonalToken_Is403()
    {
        var clerk = _factory.CreateAuthenticatedClient();
        var workoutId = await CreateWorkoutWithWalkAsync(clerk);
        var walk = await WalkAsync(clerk);
        var created = await clerk.PostAsJsonAsync("/api/v1/users/me/api-tokens", new { name = "Terran CC" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var token = (await created.Content.ReadFromJsonAsync<CreatedApiTokenDto>())!.Token;
        var tokenClient = _factory.CreateClient();
        tokenClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {token}");

        var response = await tokenClient.PutAsJsonAsync(
            $"/api/v1/workouts/{workoutId}/exercises/{walk.Id}/cardio-settings", new { intensity = 5m, speed = 5.5m });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WalkAsync(clerk)).Progression.Should().BeOfType<CardioStageProgressionDto>()
            .Which.IsSettingsPending.Should().BeTrue();
    }
}
