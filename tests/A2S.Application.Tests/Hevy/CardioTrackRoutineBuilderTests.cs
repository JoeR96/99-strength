using A2S.Application.Hevy;
using A2S.Application.Interfaces.Hevy;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Application.Tests.Hevy;

public class CardioTrackRoutineBuilderTests
{
    private static readonly UserId Joe = new("user_joe_push");
    private static readonly DateOnly Start = new(2026, 11, 16);
    private static readonly DateTimeOffset At = new(2026, 11, 16, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BuiltInTemplates_MatchHevyIds()
    {
        HevyTemplateCatalogue.StairMachineFloors.Should().Be("4377A52B");
        HevyTemplateCatalogue.Walking.Should().Be("33EDD7DB");
        HevyTemplateCatalogue.Treadmill.Should().Be("243710DE");
        HevyTemplateCatalogue.Running.Should().Be("AC1BB830");

        HevyTemplateCatalogue.BuiltInTemplateFor(CardioTrackTemplates.Create(CardioTrackTemplates.StairFinisher, Joe, Start))
            .Should().Be(HevyTemplateCatalogue.StairMachineFloors);
        HevyTemplateCatalogue.BuiltInTemplateFor(CardioTrackTemplates.Create(CardioTrackTemplates.WalkBuilder, Joe, Start))
            .Should().Be(HevyTemplateCatalogue.Treadmill, "the walk builder is treadmill incline walking");
        HevyTemplateCatalogue.BuiltInTemplateFor(CardioTrackTemplates.Create(CardioTrackTemplates.WalkToRun, Joe, Start))
            .Should().Be(HevyTemplateCatalogue.Running);

        HevyTemplateCatalogue.ModalitiesForTemplate("4377A52B").Should().Equal(CardioModality.StairClimber);
        HevyTemplateCatalogue.ModalitiesForTemplate("243710DE").Should().Equal(CardioModality.Walk, CardioModality.Run);
        HevyTemplateCatalogue.IsBuiltInCardio("79D0BB3A").Should().BeFalse();
    }

    [Fact]
    public void TrackExercise_Continuous_IsOneDurationSet_WithPrescriptionInNotes()
    {
        var stairs = CardioTrackTemplates.Create(CardioTrackTemplates.StairFinisher, Joe, Start);

        var exercise = RoutineBuilder.TrackExercise(stairs);

        exercise.ExerciseTemplateId.Should().Be("4377A52B");
        exercise.Sets.Select(s => (s.Type, s.DurationSeconds)).Should().Equal(
            (HevySetTypes.Warmup, 180), (HevySetTypes.Normal, 300), (HevySetTypes.Normal, 180));
        exercise.Notes.Should().Contain("3 min easy (L3) → 5 min continuous @ level 6 → 3 min easy (L3)")
            .And.Contain("2 more good session(s) to step up");
    }

    [Fact]
    public void TrackExercise_Intervals_IsWorkThenEasyPerRound()
    {
        var stairs = CardioTrackTemplates.Create(CardioTrackTemplates.StairFinisher, Joe, Start);
        var day = Start;
        for (var i = 0; i < 120 && stairs.State.Suggestion == TrackSuggestion.None; i++)
        {
            day = day.AddDays(1);
            var minutes = (int)stairs.State.Volume;
            stairs.LogSession($"s{i}", day, new CardioLog(minutes * 60, null, null, null, null, minutes, effort: EffortRating.Brutal), true, null, At);
        }

        stairs.AcceptSuggestion(At);

        var exercise = RoutineBuilder.TrackExercise(stairs);
        exercise.Sets.Should().HaveCount(14, "a warm-up set, 6 rounds of hard then easy, and a cool-down set");
        exercise.Sets[0].Type.Should().Be(HevySetTypes.Warmup);
        exercise.Sets.Skip(1).Take(12).Should().AllSatisfy(s => s.DurationSeconds.Should().Be(60));
        exercise.Notes.Should().Contain("6 × 1 min hard / 1 min easy @ level 10");
    }

    [Fact]
    public void WithFinishers_AppendsTheFinisherLast()
    {
        var stairs = CardioTrackTemplates.Create(CardioTrackTemplates.StairFinisher, Joe, Start);
        var lift = new HevyRoutineDraft
        {
            Title = "Upper A",
            Exercises = [new HevyRoutineExerciseDraft { ExerciseTemplateId = "79D0BB3A" }]
        };

        var withFinisher = RoutineBuilder.WithFinishers(lift, [stairs]);

        withFinisher.Exercises.Select(e => e.ExerciseTemplateId).Should().Equal("79D0BB3A", "4377A52B");
        RoutineBuilder.WithFinishers(lift, []).Should().BeSameAs(lift);
    }

    [Fact]
    public void Track_RoutineForACardioSlot_UsesTheTrack()
    {
        var walk = CardioTrackTemplates.Create(CardioTrackTemplates.WalkBuilder, Joe, Start);

        var routine = RoutineBuilder.Track("Plan · Walk", 7, walk);

        routine.Notes.Should().Be("20 min continuous");
        routine.FolderId.Should().Be(7);
        routine.Exercises.Should().ContainSingle().Which.Sets.Single().DurationSeconds.Should().Be(1200);
    }

    [Fact]
    public void LogFrom_ReadsFloorsStepsDistanceAndRpe()
    {
        var stairs = new HevyWorkoutExercise
        {
            Index = 4,
            ExerciseTemplateId = HevyTemplateCatalogue.StairMachineFloors,
            Sets = [new HevyWorkoutSet { DurationSeconds = 240, CustomMetric = 20, Rpe = 8 }, new HevyWorkoutSet { DurationSeconds = 120, CustomMetric = 9 }]
        };
        var empty = new HevyWorkoutExercise { ExerciseTemplateId = HevyTemplateCatalogue.Walking, Sets = [new HevyWorkoutSet { DistanceMeters = 500 }] };

        var log = HevyCardioTrackLogger.LogFrom(stairs)!;

        log.DurationSeconds.Should().Be(360);
        log.Floors.Should().Be(29);
        log.Steps.Should().BeNull();
        log.Rpe.Should().Be(8);
        log.Source.Should().Be(CardioLogSource.Hevy);
        HevyCardioTrackLogger.LogFrom(empty).Should().BeNull("no duration, nothing to judge");
        HevyCardioTrackLogger.Key("w1", 4).Should().Be("hevy:w1:4");
    }
}
