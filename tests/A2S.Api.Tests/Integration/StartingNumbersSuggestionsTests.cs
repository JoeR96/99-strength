using System.Net;
using System.Net.Http.Json;
using A2S.Api.Contracts.Requests;
using A2S.Application.Commands.SuggestedPlans;
using A2S.Application.Queries.Suggestions;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>Starting-number suggestions over HTTP: without a Hevy key every pending lift falls back to manual input.</summary>
[Collection("Integration")]
public class StartingNumbersSuggestionsTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public StartingNumbersSuggestionsTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Lists_pending_lifts_with_no_suggestion_when_hevy_is_not_connected()
    {
        var client = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var applied = await client.PostAsJsonAsync("/api/v1/plans/templates/plan-v3/apply",
            new ApplyPlanTemplateRequest(null, new Dictionary<string, decimal?>(), null, false));
        var workoutId = (await applied.Content.ReadFromJsonAsync<StartSuggestedPlanResult>())!.WorkoutId!.Value;

        var dto = await client.GetFromJsonAsync<StartingNumbersSuggestionsDto>($"/api/v1/workouts/{workoutId}/suggestions/starting-numbers");

        dto!.HevyStatus.Should().Be("noKey");
        dto.Items.Should().Contain(i => i.ExerciseName == "Front Squat" && i.Kind == "TrainingMax");
        dto.Items.Should().OnlyContain(i => i.Suggestion == null);
    }

    [Fact]
    public async Task Another_users_workout_is_not_found()
    {
        var owner = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");
        var applied = await owner.PostAsJsonAsync("/api/v1/plans/templates/plan-v3/apply",
            new ApplyPlanTemplateRequest(null, new Dictionary<string, decimal?>(), null, false));
        var workoutId = (await applied.Content.ReadFromJsonAsync<StartSuggestedPlanResult>())!.WorkoutId!.Value;
        var other = _factory.CreateAuthenticatedClient($"user_{Guid.NewGuid():N}");

        var response = await other.GetAsync($"/api/v1/workouts/{workoutId}/suggestions/starting-numbers");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
