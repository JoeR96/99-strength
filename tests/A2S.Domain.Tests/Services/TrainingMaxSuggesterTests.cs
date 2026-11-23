using A2S.Domain.Enums;
using A2S.Domain.Services;
using A2S.Domain.ValueObjects;
using FluentAssertions;

namespace A2S.Domain.Tests.Services;

public class TrainingMaxSuggesterTests
{
    private static readonly DateOnly Today = new(2026, 11, 23);
    private static readonly DateOnly Nov14 = new(2026, 11, 14);

    [Fact]
    public void Suggests_ninety_percent_of_the_best_e1rm_rounded_down_to_2_5_kg()
    {
        var sets = new[]
        {
            new DatedSet(Nov14, 75m, 5),
            new DatedSet(Nov14, 70m, 5)
        };

        var suggestion = TrainingMaxSuggester.ForTrainingMax(sets, Today);

        suggestion.Should().NotBeNull();
        suggestion!.ValueKg.Should().Be(77.5m);
        suggestion.Kind.Should().Be(StartingNumberKind.TrainingMax);
        suggestion.Stale.Should().BeFalse();
        suggestion.Evidence.Set.Should().Be(new DatedSet(Nov14, 75m, 5));
        suggestion.Evidence.EstimatedOneRepMaxKg.Should().Be(87.5m);
        suggestion.Evidence.WindowStart.Should().Be(Today.AddDays(-90));
        suggestion.Evidence.WindowEnd.Should().Be(Today);
        suggestion.Reason.Should().Be("From 75 kg × 5 on 14 Nov (est. 1RM 87.5 kg)");
    }

    [Fact]
    public void Ignores_warmups_and_sets_outside_one_to_twelve_reps()
    {
        var sets = new[]
        {
            new DatedSet(Nov14, 120m, 5, IsWarmup: true),
            new DatedSet(Nov14, 60m, 20),
            new DatedSet(Nov14, 0m, 8),
            new DatedSet(Nov14, 60m, 10)
        };

        var suggestion = TrainingMaxSuggester.ForTrainingMax(sets, Today);

        suggestion!.Evidence.Set.Should().Be(new DatedSet(Nov14, 60m, 10));
        suggestion.ValueKg.Should().Be(70m);
    }

    [Fact]
    public void A_single_counts_at_face_value()
    {
        var suggestion = TrainingMaxSuggester.ForTrainingMax([new DatedSet(Nov14, 100m, 1)], Today);

        suggestion!.Evidence.EstimatedOneRepMaxKg.Should().Be(100m);
        suggestion.ValueKg.Should().Be(90m);
    }

    [Fact]
    public void No_history_means_no_suggestion()
    {
        TrainingMaxSuggester.ForTrainingMax([], Today).Should().BeNull();
        TrainingMaxSuggester.ForTrainingMax([new DatedSet(Nov14, 60m, 5, IsWarmup: true)], Today).Should().BeNull();
    }

    [Fact]
    public void Older_history_uses_the_most_recent_session_and_takes_five_percent_per_three_months()
    {
        var lastSession = Today.AddDays(-200);
        var sets = new[]
        {
            new DatedSet(Today.AddDays(-400), 140m, 3),
            new DatedSet(lastSession, 100m, 1),
            new DatedSet(lastSession, 90m, 3)
        };

        var suggestion = TrainingMaxSuggester.ForTrainingMax(sets, Today);

        suggestion!.Stale.Should().BeTrue();
        suggestion.StalePenaltyPercent.Should().Be(10);
        suggestion.Evidence.Set.Should().Be(new DatedSet(lastSession, 100m, 1));
        suggestion.ValueKg.Should().Be(80m);
        suggestion.Reason.Should().StartWith("Older history");
    }

    [Fact]
    public void Stale_penalty_is_capped_at_twenty_percent()
    {
        var suggestion = TrainingMaxSuggester.ForTrainingMax([new DatedSet(Today.AddDays(-800), 100m, 1)], Today);

        suggestion!.StalePenaltyPercent.Should().Be(20);
        suggestion.ValueKg.Should().Be(70m);
    }

    [Fact]
    public void Day_ninety_is_still_inside_the_window()
    {
        var suggestion = TrainingMaxSuggester.ForTrainingMax([new DatedSet(Today.AddDays(-90), 100m, 1)], Today);

        suggestion!.Stale.Should().BeFalse();
    }

    [Fact]
    public void Starting_weight_for_reps_per_set_uses_the_mid_reps_and_the_barbell_increment()
    {
        var suggestion = TrainingMaxSuggester.ForStartingWeight(
            [new DatedSet(Nov14, 75m, 5)], Today, EquipmentType.Cable, RepRange.Create(8, 12));

        suggestion!.Kind.Should().Be(StartingNumberKind.StartingWeight);
        suggestion.ValueKg.Should().Be(65m);
    }

    [Theory]
    [InlineData(30, 28)]
    [InlineData(12, 10)]
    [InlineData(7, 6)]
    public void Starting_weight_for_dumbbells_rounds_down_to_a_real_dumbbell(decimal weight, decimal expected)
    {
        var suggestion = TrainingMaxSuggester.ForStartingWeight(
            [new DatedSet(Nov14, weight, 8)], Today, EquipmentType.Dumbbell, RepRange.Create(8, 12));

        suggestion!.ValueKg.Should().Be(expected);
    }

    [Fact]
    public void Bodyweight_exercises_get_no_starting_weight()
    {
        TrainingMaxSuggester.ForStartingWeight([new DatedSet(Nov14, 10m, 8)], Today, EquipmentType.Bodyweight, RepRange.Create(8, 12))
            .Should().BeNull();
    }
}
