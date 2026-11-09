using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using A2S.Application.Interfaces.Hevy;
using A2S.Integration.Hevy.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace A2S.Integration.Hevy.Tests;

/// <summary>
/// Contract tests for <see cref="HevyClient"/> against recorded fixtures: wire shapes, error
/// mapping (401/403/429/503/timeout), retry only for GETs, and the 409 → PUT fallback.
/// </summary>
public class HevyClientTests
{
    private static readonly DateTimeOffset Since = new(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeHevyHandler _handler = new();
    private readonly HevyClient _client;

    public HevyClientTests()
    {
        _client = CreateClient(_handler);
    }

    private static HevyClient CreateClient(FakeHevyHandler handler, Action<HevyOptions>? configure = null)
    {
        var options = new HevyOptions { RetryBaseDelayMilliseconds = 1, RetryCount = 3, MaxRetryAfterSeconds = 0, CircuitBreakerThreshold = 50 };
        configure?.Invoke(options);
        return new HevyClient(new SingleClientFactory(handler), Options.Create(options), NullLogger<HevyClient>.Instance);
    }

    [Fact]
    public async Task GetWorkoutEvents_ReadsUpdatedAndDeletedEvents_AndSendsTheKeyHeader()
    {
        _handler.EnqueueFixture(HttpStatusCode.OK, "workout-events-page.json");

        var result = await _client.GetWorkoutEventsAsync(FakeHevyHandler.TestApiKey, Since, page: 1);

        result.Ok.Should().BeTrue();
        var page = result.Value!;
        page.Page.Should().Be(1);
        page.PageCount.Should().Be(2);
        page.HasMore.Should().BeTrue();
        page.Items.Should().HaveCount(2);

        var updated = page.Items[0];
        updated.IsUpdated.Should().BeTrue();
        updated.Workout!.Id.Should().Be("b3c2a1d0-1111-4222-8333-444455556666");
        updated.Workout.RoutineId.Should().Be("7f6e5d4c-aaaa-4bbb-8ccc-ddddeeeeffff");
        updated.Workout.StartTime.Should().Be(new DateTimeOffset(2026, 11, 16, 17, 30, 0, TimeSpan.Zero));
        updated.Workout.Exercises.Should().HaveCount(2);
        updated.Workout.Exercises[1].Sets.Where(s => HevySetTypes.IsWorking(s.Type)).Should().HaveCount(4);
        updated.Workout.Exercises[1].Sets[^1].Type.Should().Be(HevySetTypes.Failure);
        updated.Workout.Exercises[1].Sets[^1].Reps.Should().Be(9);
        updated.OccurredAt.Should().Be(new DateTimeOffset(2026, 11, 16, 18, 41, 12, TimeSpan.Zero));

        var deleted = page.Items[1];
        deleted.IsDeleted.Should().BeTrue();
        deleted.Id.Should().Be("0e0d0c0b-2222-4333-8444-555566667777");
        deleted.OccurredAt.Should().Be(new DateTimeOffset(2026, 11, 15, 9, 12, 0, TimeSpan.Zero));

        var request = _handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.ApiKey.Should().Be("test");
        request.Uri.AbsolutePath.Should().EndWith("/workouts/events");
        request.Uri.Query.Should().Contain("since=2026-11-15T00%3A00%3A00.000Z").And.Contain("page=1").And.Contain("pageSize=10");
    }

    [Fact]
    public async Task CreateRoutine_PostsWrappedBody_AndReadsTheWrappedRoutine()
    {
        _handler.EnqueueFixture(HttpStatusCode.Created, "routine-created.json");
        var draft = new HevyRoutineDraft
        {
            Title = "Cut to 85 · W3 Upper A",
            FolderId = 42,
            Exercises =
            [
                new HevyRoutineExerciseDraft { ExerciseTemplateId = "79D0BB3A", RestSeconds = 180, Sets = [new HevySetDraft { Type = HevySetTypes.Failure, WeightKg = 80m, Reps = 5 }] }
            ]
        };

        var result = await _client.CreateRoutineAsync(FakeHevyHandler.TestApiKey, draft);

        result.Ok.Should().BeTrue();
        result.Value!.Id.Should().Be("7f6e5d4c-aaaa-4bbb-8ccc-ddddeeeeffff");
        result.Value.Title.Should().Be("Cut to 85 · W3 Upper A");

        var request = _handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.AbsolutePath.Should().EndWith("/routines");
        using var body = JsonDocument.Parse(request.Body!);
        var routine = body.RootElement.GetProperty("routine");
        routine.GetProperty("title").GetString().Should().Be("Cut to 85 · W3 Upper A");
        routine.GetProperty("folder_id").GetInt32().Should().Be(42);
        var set = routine.GetProperty("exercises")[0].GetProperty("sets")[0];
        set.GetProperty("type").GetString().Should().Be("failure");
        set.GetProperty("weight_kg").GetDecimal().Should().Be(80m);
        set.TryGetProperty("duration_seconds", out _).Should().BeFalse("nulls are left out of the payload");
    }

    [Fact]
    public async Task UpdateRoutine_PutsToTheRoutineId()
    {
        _handler.EnqueueFixture(HttpStatusCode.OK, "routine-created.json");

        var result = await _client.UpdateRoutineAsync(FakeHevyHandler.TestApiKey, "7f6e5d4c-aaaa-4bbb-8ccc-ddddeeeeffff", new HevyRoutineDraft { Title = "x" });

        result.Ok.Should().BeTrue();
        var request = _handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Put);
        request.Uri.AbsolutePath.Should().EndWith("/routines/7f6e5d4c-aaaa-4bbb-8ccc-ddddeeeeffff");
    }

    [Fact]
    public async Task GetRoutines_ReadsTheRoutinesPage()
    {
        _handler.EnqueueFixture(HttpStatusCode.OK, "routines-page.json");

        var result = await _client.GetRoutinesAsync(FakeHevyHandler.TestApiKey, 1);

        result.Ok.Should().BeTrue();
        result.Value!.Items.Select(r => r.Title).Should().Equal("Cut to 85 · W3 Upper A", "Cut to 85 · Norwegian 4×4");
        result.Value.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GetRoutineFolders_ReadsFolders_AndCreateFolderWrapsTheTitle()
    {
        _handler.EnqueueFixture(HttpStatusCode.OK, "routine-folders-page.json");
        _handler.Enqueue(HttpStatusCode.Created, """{ "routine_folder": { "id": 43, "title": "New plan" } }""");

        var folders = await _client.GetRoutineFoldersAsync(FakeHevyHandler.TestApiKey, 1);
        var created = await _client.CreateRoutineFolderAsync(FakeHevyHandler.TestApiKey, "New plan");

        folders.Value!.Items.Should().ContainSingle(f => f.Id == 42 && f.Title == "Cut to 85");
        created.Ok.Should().BeTrue();
        created.Value!.Id.Should().Be(43);
        _handler.Requests[1].Body.Should().Be("""{"routine_folder":{"title":"New plan"}}""");
    }

    [Fact]
    public async Task CreateExerciseTemplate_AcceptsABareObjectResponse()
    {
        _handler.EnqueueFixture(HttpStatusCode.Created, "exercise-template-created.json");

        var result = await _client.CreateExerciseTemplateAsync(FakeHevyHandler.TestApiKey, new HevyExerciseTemplateDraft
        {
            Title = "90/90 Hip Switch",
            ExerciseType = HevyExerciseTypes.RepsOnly,
            PrimaryMuscleGroup = "glutes"
        });

        result.Ok.Should().BeTrue();
        result.Value!.Id.Should().Be("C0FFEE01");
        using var body = JsonDocument.Parse(_handler.Requests[0].Body!);
        var template = body.RootElement.GetProperty("exercise_template");
        template.GetProperty("exercise_type").GetString().Should().Be("reps_only");
        template.GetProperty("primary_muscle_group").GetString().Should().Be("glutes");
    }

    [Fact]
    public async Task UpsertBodyMeasurement_On409_FallsBackToPutForThatDate()
    {
        _handler.Enqueue(HttpStatusCode.Conflict, """{ "error": "A measurement already exists for this date" }""");
        _handler.EnqueueFixture(HttpStatusCode.OK, "body-measurement-created.json");

        var result = await _client.UpsertBodyMeasurementAsync(FakeHevyHandler.TestApiKey, new DateOnly(2026, 11, 16), 94.3m);

        result.Ok.Should().BeTrue();
        result.Value!.WeightKg.Should().Be(94.3m);
        _handler.Requests.Should().HaveCount(2);
        _handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        _handler.Requests[0].Uri.AbsolutePath.Should().EndWith("/body_measurements");
        _handler.Requests[1].Method.Should().Be(HttpMethod.Put);
        _handler.Requests[1].Uri.AbsolutePath.Should().EndWith("/body_measurements/2026-11-16");
        _handler.Requests[1].Body.Should().Contain("\"weight_kg\":94.3");
    }

    [Fact]
    public async Task UpsertBodyMeasurement_On201_DoesNotPut()
    {
        _handler.EnqueueFixture(HttpStatusCode.Created, "body-measurement-created.json");

        var result = await _client.UpsertBodyMeasurementAsync(FakeHevyHandler.TestApiKey, new DateOnly(2026, 11, 16), 94.3m);

        result.Ok.Should().BeTrue();
        _handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Unauthorized_MapsToInvalidKey()
    {
        _handler.Enqueue(HttpStatusCode.Unauthorized, """{ "error": "Unauthorized" }""");

        var result = await _client.GetRoutinesAsync(FakeHevyHandler.TestApiKey, 1);

        result.Ok.Should().BeFalse();
        result.Error!.Kind.Should().Be(HevyErrorKind.InvalidKey);
        result.Error.Message.Should().Be("Hevy says the key is invalid");
        _handler.Requests.Should().ContainSingle("a 401 is final, not retried");
    }

    [Fact]
    public async Task Forbidden_MapsToProNeeded()
    {
        _handler.Enqueue(HttpStatusCode.Forbidden, """{ "error": "Pro required" }""");

        var result = await _client.CreateRoutineAsync(FakeHevyHandler.TestApiKey, new HevyRoutineDraft { Title = "x" });

        result.Error!.Kind.Should().Be(HevyErrorKind.ProNeeded);
        result.Error.Message.Should().Be("Hevy Pro is required for the API");
    }

    [Fact]
    public async Task TooManyRequests_WithRetryAfter_IsReportedWithTheWait()
    {
        _handler.Enqueue(HttpStatusCode.TooManyRequests, null, r => r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7)));

        var result = await _client.GetRoutinesAsync(FakeHevyHandler.TestApiKey, 1);

        result.Error!.Kind.Should().Be(HevyErrorKind.RateLimited);
        result.Error.RetryAfter.Should().Be(TimeSpan.FromSeconds(7));
        result.Error.Message.Should().Be("Hevy is rate limiting, try again in 7 s");
        _handler.Requests.Should().ContainSingle("a 7 s Retry-After is longer than the client waits out");
    }

    [Fact]
    public async Task TooManyRequests_WithShortRetryAfter_IsWaitedOutAndRetried_ForGet()
    {
        var handler = new FakeHevyHandler()
            .Enqueue(HttpStatusCode.TooManyRequests, null, r => r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero))
            .EnqueueFixture(HttpStatusCode.OK, "routines-page.json");
        var client = CreateClient(handler, o => o.MaxRetryAfterSeconds = 1);

        var result = await client.GetRoutinesAsync(FakeHevyHandler.TestApiKey, 1);

        result.Ok.Should().BeTrue();
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ServiceUnavailable_OnGet_IsRetriedThreeTimes_ThenUnavailable()
    {
        _handler.Enqueue(HttpStatusCode.ServiceUnavailable)
            .Enqueue(HttpStatusCode.ServiceUnavailable)
            .Enqueue(HttpStatusCode.ServiceUnavailable)
            .Enqueue(HttpStatusCode.ServiceUnavailable);

        var result = await _client.GetRoutinesAsync(FakeHevyHandler.TestApiKey, 1);

        result.Error!.Kind.Should().Be(HevyErrorKind.Unavailable);
        result.Error.Message.Should().Be("Hevy is unavailable, your change is saved locally");
        _handler.Requests.Should().HaveCount(4, "one call plus three retries");
    }

    [Fact]
    public async Task ServiceUnavailable_OnGet_RecoversWhenARetrySucceeds()
    {
        _handler.Enqueue(HttpStatusCode.ServiceUnavailable).EnqueueFixture(HttpStatusCode.OK, "routines-page.json");

        var result = await _client.GetRoutinesAsync(FakeHevyHandler.TestApiKey, 1);

        result.Ok.Should().BeTrue();
        _handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ServiceUnavailable_OnPost_IsNotRetried()
    {
        _handler.Enqueue(HttpStatusCode.ServiceUnavailable);

        var result = await _client.CreateRoutineAsync(FakeHevyHandler.TestApiKey, new HevyRoutineDraft { Title = "x" });

        result.Error!.Kind.Should().Be(HevyErrorKind.Unavailable);
        _handler.Requests.Should().ContainSingle("a write that may have gone through must not be replayed");
    }

    [Fact]
    public async Task Timeout_OnPost_IsNotRetried_AndMapsToUnavailable()
    {
        _handler.EnqueueTimeout();

        var result = await _client.CreateRoutineAsync(FakeHevyHandler.TestApiKey, new HevyRoutineDraft { Title = "x" });

        result.Ok.Should().BeFalse();
        result.Error!.Kind.Should().Be(HevyErrorKind.Unavailable);
        _handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Timeout_OnGet_IsRetried()
    {
        _handler.EnqueueTimeout().EnqueueFixture(HttpStatusCode.OK, "routines-page.json");

        var result = await _client.GetRoutinesAsync(FakeHevyHandler.TestApiKey, 1);

        result.Ok.Should().BeTrue();
        _handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task NetworkFailure_MapsToUnavailable()
    {
        _handler.EnqueueNetworkFailure().EnqueueNetworkFailure().EnqueueNetworkFailure().EnqueueNetworkFailure();

        var result = await _client.GetRoutinesAsync(FakeHevyHandler.TestApiKey, 1);

        result.Error!.Kind.Should().Be(HevyErrorKind.Unavailable);
    }

    [Fact]
    public async Task BadRequest_CarriesHevysMessage()
    {
        _handler.Enqueue(HttpStatusCode.BadRequest, """{ "error": "exercise_template_id is required" }""");

        var result = await _client.CreateRoutineAsync(FakeHevyHandler.TestApiKey, new HevyRoutineDraft { Title = "x" });

        result.Error!.Kind.Should().Be(HevyErrorKind.BadRequest);
        result.Error.Message.Should().Be("Hevy rejected the request: exercise_template_id is required");
    }

    [Fact]
    public async Task NotFound_OnPut_MapsToNotFound()
    {
        _handler.Enqueue(HttpStatusCode.NotFound, """{ "error": "Routine not found" }""");

        var result = await _client.UpdateRoutineAsync(FakeHevyHandler.TestApiKey, "gone", new HevyRoutineDraft { Title = "x" });

        result.Error!.Kind.Should().Be(HevyErrorKind.NotFound);
    }

    [Fact]
    public async Task OpenCircuit_ForOneKey_DoesNotAffectAnotherKey()
    {
        var handler = new FakeHevyHandler();
        for (var i = 0; i < 2; i++)
        {
            handler.Enqueue(HttpStatusCode.ServiceUnavailable);
        }
        handler.EnqueueFixture(HttpStatusCode.OK, "routines-page.json");
        var client = CreateClient(handler, o =>
        {
            o.RetryCount = 0;
            o.CircuitBreakerThreshold = 2;
            o.CircuitBreakerDurationSeconds = 60;
        });

        var first = await client.GetRoutinesAsync("key-one", 1);
        var second = await client.GetRoutinesAsync("key-one", 1);
        var third = await client.GetRoutinesAsync("key-one", 1);
        var other = await client.GetRoutinesAsync("key-two", 1);

        first.Error!.Kind.Should().Be(HevyErrorKind.Unavailable);
        second.Error!.Kind.Should().Be(HevyErrorKind.Unavailable);
        third.Error!.Kind.Should().Be(HevyErrorKind.Unavailable);
        other.Ok.Should().BeTrue();
        handler.Requests.Should().HaveCount(3, "the third call for key-one was short-circuited without hitting Hevy");
    }

    [Fact]
    public async Task ValidateKey_ReturnsFalseOnUnauthorized_TrueOnOk()
    {
        _handler.Enqueue(HttpStatusCode.Unauthorized).Enqueue(HttpStatusCode.OK, """{ "workout_count": 12 }""");

        var bad = await _client.ValidateKeyAsync("wrong");
        var good = await _client.ValidateKeyAsync(FakeHevyHandler.TestApiKey);

        bad.Ok.Should().BeTrue();
        bad.Value.Should().BeFalse();
        good.Value.Should().BeTrue();
    }

    [Fact]
    public async Task BlankKey_FailsBeforeAnyRequest()
    {
        var result = await _client.GetRoutinesAsync(" ", 1);

        result.Error!.Kind.Should().Be(HevyErrorKind.InvalidKey);
        _handler.Requests.Should().BeEmpty();
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(15) };
    }
}
