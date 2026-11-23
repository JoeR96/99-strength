using System.Text.Json;
using A2S.Application.Common;
using A2S.Application.Hevy;
using A2S.Application.Interfaces;
using A2S.Application.Interfaces.Hevy;
using A2S.Application.Queries.Suggestions;
using A2S.Application.Services;
using A2S.Application.Tests.Hevy;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace A2S.Application.Tests.Queries.Suggestions;

public class GetStartingNumbersSuggestionsQueryHandlerTests
{
    private const string Key = "fake-key";
    private const string SquatTemplate = "D04AC939";
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly LocalClock _clock = new(new FakeTimeProvider(Now));
    private readonly IHevyClient _hevy = Substitute.For<IHevyClient>();
    private readonly IHevyKeyResolver _keys = Substitute.For<IHevyKeyResolver>();
    private readonly InMemorySnapshots _snapshots = new();
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly Exercise _squat;
    private readonly Exercise _bench;
    private readonly Exercise _curl;
    private readonly Exercise _settled;
    private readonly Workout _workout;
    private readonly GetStartingNumbersSuggestionsQueryHandler _handler;

    public GetStartingNumbersSuggestionsQueryHandlerTests()
    {
        _currentUser.UserId.Returns(Joe.Value);
        _keys.ForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns(Key);
        _squat = Exercise.CreateWithPendingTrainingMax("Squat (Barbell)", ExerciseCategory.MainLift, EquipmentType.Barbell, DayNumber.Day1, 1, SquatTemplate);
        _bench = Exercise.CreateWithPendingTrainingMax("Bench Press (Barbell)", ExerciseCategory.MainLift, EquipmentType.Barbell, DayNumber.Day1, 2, "79D0BB3A");
        _curl = Exercise.CreateWithRepsPerSetProgression("Bicep Curl (Dumbbell)", ExerciseCategory.Accessory, EquipmentType.Dumbbell, DayNumber.Day1, 3, "custom:Bicep Curl Hammer", RepRange.Create(8, 12));
        _settled = Exercise.CreateWithRepsPerSetProgression("Leg Press", ExerciseCategory.Accessory, EquipmentType.Machine, DayNumber.Day1, 4, "LP1", RepRange.Create(8, 12), startingWeight: Weight.Create(100, WeightUnit.Kilograms));
        _workout = Workout.Create(Joe, "Test", ProgramVariant.FourDay, [_squat, _bench, _curl, _settled]);
        _workouts.GetByIdAsync(_workout.Id, Arg.Any<CancellationToken>()).Returns(_workout);

        var links = new InMemoryLinks();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var catalogue = new HevyTemplateCatalogue(_hevy, links, Substitute.For<IExerciseLibraryProvider>(), unitOfWork, _clock);
        var history = new HevyStrengthHistory(_hevy, _keys, _snapshots, new MemoryCache(new MemoryCacheOptions()), _clock);
        _handler = new GetStartingNumbersSuggestionsQueryHandler(_workouts, _currentUser, history, catalogue, _clock);
    }

    private static HevyWorkout Session(string id, DateTimeOffset start, params HevyWorkoutExercise[] exercises) => new()
    {
        Id = id,
        Title = "Lift",
        StartTime = start,
        EndTime = start.AddHours(1),
        Exercises = exercises
    };

    private static HevyWorkoutExercise Lift(string templateId, string title, params (string Type, decimal Kg, int Reps)[] sets) => new()
    {
        ExerciseTemplateId = templateId,
        Title = title,
        Sets = sets.Select((s, i) => new HevyWorkoutSet { Index = i, Type = s.Type, WeightKg = s.Kg, Reps = s.Reps }).ToList()
    };

    private void HevyReturns(params HevyWorkout[] workouts) =>
        _hevy.GetWorkoutsAsync(Key, 1, Arg.Any<CancellationToken>())
            .Returns(HevyResult<HevyPage<HevyWorkout>>.Success(new HevyPage<HevyWorkout> { Page = 1, PageCount = 1, Items = workouts }));

    private async Task<StartingNumbersSuggestionsDto> Run()
    {
        var result = await _handler.Handle(new GetStartingNumbersSuggestionsQuery(_workout.Id.Value), CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value;
    }

    [Fact]
    public async Task Lists_only_pending_exercises_and_suggests_from_template_id_ignoring_warmups()
    {
        HevyReturns(Session("w1", new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero),
            Lift(SquatTemplate, "Renamed in Hevy", ("warmup", 120m, 5), ("normal", 75m, 5), ("normal", 70m, 5))));

        var dto = await Run();

        dto.HevyStatus.Should().Be("ok");
        dto.Items.Select(i => i.ExerciseId).Should().Equal(_squat.Id.Value, _bench.Id.Value, _curl.Id.Value);
        var squat = dto.Items[0];
        squat.Kind.Should().Be("TrainingMax");
        squat.MatchedBy.Should().Be("templateId");
        squat.Suggestion!.ValueKg.Should().Be(77.5m);
        squat.Suggestion.SetWeightKg.Should().Be(75m);
        squat.Suggestion.SetDate.Should().Be(new DateOnly(2026, 11, 14));
        squat.Suggestion.Reason.Should().Be("From 75 kg × 5 on 14 Nov (est. 1RM 87.5 kg)");
        dto.Items[1].Suggestion.Should().BeNull();
    }

    [Fact]
    public async Task Falls_back_to_exact_title_then_catalogue_name()
    {
        HevyReturns(Session("w1", new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero),
            Lift("OTHER-ID", "bench press (barbell)", ("normal", 100m, 1)),
            Lift("CUSTOM-ID", "Bicep Curl Hammer", ("normal", 16m, 10))));

        var dto = await Run();

        dto.Items[1].MatchedBy.Should().Be("title");
        dto.Items[1].Suggestion!.ValueKg.Should().Be(90m);
        dto.Items[2].MatchedBy.Should().Be("catalogueName");
        dto.Items[2].Kind.Should().Be("StartingWeight");
        dto.Items[2].Suggestion!.ValueKg.Should().Be(14m);
    }

    [Fact]
    public async Task Old_only_history_is_flagged_stale()
    {
        HevyReturns(Session("w-old", new DateTimeOffset(2026, 5, 7, 10, 0, 0, TimeSpan.Zero),
            Lift(SquatTemplate, "Squat (Barbell)", ("normal", 100m, 1))));

        var dto = await Run();

        dto.Items[0].Suggestion!.Stale.Should().BeTrue();
        dto.Items[0].Suggestion!.StalePenaltyPercent.Should().Be(10);
        dto.Items[0].Suggestion!.ValueKg.Should().Be(80m);
    }

    [Fact]
    public async Task No_history_gives_no_suggestions()
    {
        HevyReturns();

        var dto = await Run();

        dto.HevyStatus.Should().Be("ok");
        dto.Items.Should().OnlyContain(i => i.Suggestion == null && i.MatchedBy == null);
    }

    [Fact]
    public async Task Hevy_failure_reports_unavailable_but_still_uses_stored_snapshots()
    {
        _hevy.GetWorkoutsAsync(Key, 1, Arg.Any<CancellationToken>())
            .Returns(HevyResult<HevyPage<HevyWorkout>>.Failure(HevyError.Unavailable()));
        var stored = Session("w-stored", new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero),
            Lift(SquatTemplate, "Squat (Barbell)", ("normal", 100m, 1)));
        var snapshot = HevyWorkoutSnapshot.Create(new HevyWorkoutId(stored.Id), Joe, stored.Title, stored.StartTime, stored.EndTime, null,
            JsonSerializer.Serialize(stored), new DateOnly(2026, 11, 20), Now);
        snapshot.MarkUnmatched("rest day", Now);
        await _snapshots.AddAsync(snapshot);

        var dto = await Run();

        dto.HevyStatus.Should().Be("unavailable");
        dto.HevyMessage.Should().Be("Hevy unavailable");
        dto.Items[0].Suggestion!.ValueKg.Should().Be(90m);
    }

    [Fact]
    public async Task No_key_means_manual_input()
    {
        _keys.ForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns((string?)null);

        var dto = await Run();

        dto.HevyStatus.Should().Be("noKey");
        dto.Items.Should().HaveCount(3).And.OnlyContain(i => i.Suggestion == null);
        await _hevy.DidNotReceive().GetWorkoutsAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pages_until_a_year_back_and_caches_per_user()
    {
        _hevy.GetWorkoutsAsync(Key, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => HevyResult<HevyPage<HevyWorkout>>.Success(new HevyPage<HevyWorkout>
            {
                Page = call.ArgAt<int>(1),
                PageCount = 50,
                Items = [Session($"w{call.ArgAt<int>(1)}", Now.AddDays(-200 * call.ArgAt<int>(1)))]
            }));

        await Run();
        await Run();

        await _hevy.Received(2).GetWorkoutsAsync(Key, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Someone_elses_workout_is_not_found()
    {
        _currentUser.UserId.Returns("user_other");

        var result = await _handler.Handle(new GetStartingNumbersSuggestionsQuery(_workout.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.NotFound);
    }
}
