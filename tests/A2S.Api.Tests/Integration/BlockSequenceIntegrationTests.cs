using System.Net;
using System.Net.Http.Json;
using A2S.Application.Commands.CreateWorkout;
using A2S.Application.Commands.UpdateBlockSequence;
using A2S.Application.DTOs;
using A2S.Application.Queries.GetWeekPlan;
using A2S.Domain.Enums;
using A2S.Tests.Shared;
using FluentAssertions;

namespace A2S.Api.Tests.Integration;

/// <summary>
/// Integration tests for block sequence management.
/// Tests CRUD operations and week plan translation with custom block sequences.
/// </summary>
[Collection("Integration")]
public class BlockSequenceIntegrationTests
{
    private readonly TestWebApplicationFactory<Program> _factory;

    public BlockSequenceIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateAuthenticatedClient();

    #region Create Workout with Block Sequence

    [Fact]
    public async Task CreateWorkout_WithCustomBlockSequence_SetsCorrectTotalWeeks()
    {
        var client = CreateClient();
        var command = new CreateWorkoutCommand(
            Name: "Block Repeat Test",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 28,
            BlockSequence: new List<int> { 1, 1, 2, 3 }
        );

        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var getCurrentResponse = await client.GetAsync("/api/v1/workouts/current");
        var workout = await getCurrentResponse.Content.ReadFromJsonAsync<WorkoutDto>();

        workout.Should().NotBeNull();
        workout!.TotalWeeks.Should().Be(28);
        workout.BlockSequence.Should().BeEquivalentTo(new[] { 1, 1, 2, 3 });
    }

    [Fact]
    public async Task CreateWorkout_WithDefaultBlockSequence_Returns21Weeks()
    {
        var client = CreateClient();
        var command = new CreateWorkoutCommand(
            Name: "Default Blocks Test",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21
        );

        var response = await client.PostAsJsonAsync("/api/v1/workouts", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var getCurrentResponse = await client.GetAsync("/api/v1/workouts/current");
        var workout = await getCurrentResponse.Content.ReadFromJsonAsync<WorkoutDto>();

        workout.Should().NotBeNull();
        workout!.TotalWeeks.Should().Be(21);
        workout.BlockSequence.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    #endregion

    #region Update Block Sequence

    [Fact]
    public async Task UpdateBlockSequence_ExtendProgram_ReturnsNewTotalWeeks()
    {
        var client = CreateClient();
        var createCommand = new CreateWorkoutCommand(
            Name: "Extend Test",
            Variant: ProgramVariant.FourDay,
            TotalWeeks: 21
        );
        var createResponse = await client.PostAsJsonAsync("/api/v1/workouts", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateWorkoutResponse>();

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/workouts/{createResult!.Id}/block-sequence",
            new { blockSequence = new[] { 1, 1, 2, 3 } }
        );

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await updateResponse.Content.ReadFromJsonAsync<UpdateBlockSequenceResult>();
        result.Should().NotBeNull();
        result!.TotalWeeks.Should().Be(28);
        result.BlockSequence.Should().BeEquivalentTo(new[] { 1, 1, 2, 3 });

        // Verify via GET
        var getCurrentResponse = await client.GetAsync("/api/v1/workouts/current");
        var workout = await getCurrentResponse.Content.ReadFromJsonAsync<WorkoutDto>();
        workout!.TotalWeeks.Should().Be(28);
        workout.BlockSequence.Should().BeEquivalentTo(new[] { 1, 1, 2, 3 });
    }

    [Fact]
    public async Task UpdateBlockSequence_InvalidBlockType_Returns400()
    {
        var client = CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/v1/workouts", new CreateWorkoutCommand(
            Name: "Invalid Block Test",
            Variant: ProgramVariant.FourDay
        ));
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateWorkoutResponse>();

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/workouts/{createResult!.Id}/block-sequence",
            new { blockSequence = new[] { 1, 4, 3 } }
        );

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateBlockSequence_EmptySequence_Returns400()
    {
        var client = CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/v1/workouts", new CreateWorkoutCommand(
            Name: "Empty Block Test",
            Variant: ProgramVariant.FourDay
        ));
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateWorkoutResponse>();

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/workouts/{createResult!.Id}/block-sequence",
            new { blockSequence = Array.Empty<int>() }
        );

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Week Plan with Block Sequence

    [Fact]
    public async Task GetWeekPlan_WithRepeatedBlock_ReturnsBlock1Parameters()
    {
        var client = CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/v1/workouts", new CreateWorkoutCommand(
            Name: "Week Plan Block Test",
            Variant: ProgramVariant.FourDay,
            BlockSequence: new List<int> { 1, 1, 2, 3 }
        ));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Template week should be 1 (Block 1, week 1 = 75% intensity)
        var weekPlanResponse = await client.GetAsync("/api/v1/workouts/weeks/8/days/1/plan");

        weekPlanResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var weekPlan = await weekPlanResponse.Content.ReadFromJsonAsync<WeekPlanDto>();
        weekPlan.Should().NotBeNull();

        // Block type should be 1 (since program week 8 maps to Block 1 in [1,1,2,3])
        weekPlan!.BlockNumber.Should().Be(1, "program week 8 in [1,1,2,3] should be Block 1");

        // Intensity should be 79% (Block 1 Week 1 — Primary tier, reps=5)
        weekPlan.IntensityPercentage.Should().Be(79m,
            "program week 8 in [1,1,2,3] maps to template week 1 (79% intensity)");
    }

    [Fact]
    public async Task GetWeekPlan_Week1_SameForBothSequences()
    {
        // Week 1 should be the same regardless of sequence (both start with Block 1)
        var client1 = CreateClient();
        await client1.PostAsJsonAsync("/api/v1/workouts", new CreateWorkoutCommand(
            Name: "Default Sequence",
            Variant: ProgramVariant.FourDay,
            BlockSequence: new List<int> { 1, 2, 3 }
        ));
        var plan1Response = await client1.GetAsync("/api/v1/workouts/weeks/1/days/1/plan");
        var plan1 = await plan1Response.Content.ReadFromJsonAsync<WeekPlanDto>();

        var client2 = CreateClient();
        await client2.PostAsJsonAsync("/api/v1/workouts", new CreateWorkoutCommand(
            Name: "Extended Sequence",
            Variant: ProgramVariant.FourDay,
            BlockSequence: new List<int> { 1, 1, 2, 3 }
        ));
        var plan2Response = await client2.GetAsync("/api/v1/workouts/weeks/1/days/1/plan");
        var plan2 = await plan2Response.Content.ReadFromJsonAsync<WeekPlanDto>();

        // Both should have the same parameters for week 1
        plan1!.IntensityPercentage.Should().Be(plan2!.IntensityPercentage);
        plan1.BlockNumber.Should().Be(plan2.BlockNumber);
    }

    #endregion
}
