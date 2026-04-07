using System.Net;
using System.Net.Http.Json;
using A2S.Application.Commands.CreateWorkout;
using A2S.Domain.Enums;
using A2S.Domain.Services;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

[Collection("Integration")]
public class SimulateWorkoutIntegrationTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public SimulateWorkoutIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    private sealed class CreateWorkoutResponseDto
    {
        public Guid Id { get; set; }
    }

    [Fact]
    public async Task Simulate_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var randomId = Guid.Parse("aaaa2222-2222-2222-2222-222222222222");

        var response = await client.GetAsync($"/api/v1/workouts/{randomId}/simulate?sessions=5");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Simulate_WhenWorkoutNotFound_ReturnsNotFound()
    {
        var client = CreateClient();
        var nonExistentId = Guid.Parse("bbbb3333-3333-3333-3333-333333333333");

        var response = await client.GetAsync($"/api/v1/workouts/{nonExistentId}/simulate?sessions=5");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Simulate_WhenValidWorkout_ReturnsSimulationResult()
    {
        var client = CreateClient();

        var createCommand = new CreateWorkoutCommand(
            Name: "Sim Test Program",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21);
        var createResponse = await client.PostAsJsonAsync("/api/v1/workouts", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateWorkoutResponseDto>();
        var workoutId = createResult!.Id;

        var activeResponse = await client.PostAsync($"/api/v1/workouts/{workoutId}/activate", null);
        activeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync($"/api/v1/workouts/{workoutId}/simulate?sessions=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SimulationResult>();
        result.Should().NotBeNull();
        result!.WorkoutName.Should().Be("Sim Test Program");
        result.ExerciseTimeSeries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Simulate_DefaultSessions_Uses30()
    {
        var client = CreateClient();

        var createCommand = new CreateWorkoutCommand(
            Name: "Default Sessions Test",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21);
        var createResponse = await client.PostAsJsonAsync("/api/v1/workouts", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateWorkoutResponseDto>();
        var workoutId = createResult!.Id;

        await client.PostAsync($"/api/v1/workouts/{workoutId}/activate", null);

        var response = await client.GetAsync($"/api/v1/workouts/{workoutId}/simulate");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SimulationResult>();
        result.Should().NotBeNull();
        // Default is 30 sessions → initial point + 30 = 31 data points
        result!.ExerciseTimeSeries[0].DataPoints.Should().HaveCount(31);
    }
}
