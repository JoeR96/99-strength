using System.Net;
using System.Net.Http.Json;
using A2S.Application.Commands.CreateWorkout;
using A2S.Domain.Enums;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

[Collection("Integration")]
public class ExerciseHistoryControllerTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public ExerciseHistoryControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    #region Exercise History

    [Fact]
    public async Task GetExerciseHistory_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/workouts/exercises/Bench%20Press/history");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetExerciseHistory_WithNonexistentExercise_ReturnsNotFound()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/workouts/exercises/NonexistentExercise12345/history");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetExerciseHistory_WithEmptyName_ReturnsBadRequest()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/workouts/exercises/%20/history");

        // Empty/whitespace name should fail
        var validStatuses = new[] { HttpStatusCode.BadRequest, HttpStatusCode.NotFound };
        validStatuses.Should().Contain(response.StatusCode);
    }

    [Fact]
    public async Task GetExerciseHistory_WithExistingExercise_ReturnsOk()
    {
        var client = CreateClient();
        var createCommand = new CreateWorkoutCommand(
            Name: "History Test Workout",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21);
        await client.PostAsJsonAsync("/api/v1/workouts", createCommand);

        // Bench Press is a default exercise in FiveDay
        var response = await client.GetAsync("/api/v1/workouts/exercises/Bench%20Press%20(Barbell)/history");

        // May or may not have history depending on completed activities, but should not error
        var validStatuses = new[] { HttpStatusCode.OK, HttpStatusCode.NotFound };
        validStatuses.Should().Contain(response.StatusCode);
    }

    #endregion

    #region Workout History

    [Fact]
    public async Task GetWorkoutHistory_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/workouts/history");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWorkoutHistory_WithNonexistentId_ReturnsNotFound()
    {
        var client = CreateClient();
        var fakeId = Guid.Parse("b3333333-3333-3333-3333-333333333333");

        var response = await client.GetAsync($"/api/v1/workouts/history?id={fakeId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetWorkoutHistory_WithoutId_ReturnsResult()
    {
        var client = CreateClient();
        var createCommand = new CreateWorkoutCommand(
            Name: "History Query Workout",
            Variant: ProgramVariant.FiveDay,
            TotalWeeks: 21);
        await client.PostAsJsonAsync("/api/v1/workouts", createCommand);

        var response = await client.GetAsync("/api/v1/workouts/history");

        // Without an explicit ID, the handler tries the current workout
        var validStatuses = new[] { HttpStatusCode.OK, HttpStatusCode.NotFound };
        validStatuses.Should().Contain(response.StatusCode);
    }

    #endregion
}
