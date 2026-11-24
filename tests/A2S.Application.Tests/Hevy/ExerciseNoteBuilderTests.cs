using A2S.Application.Hevy;
using A2S.Domain.Aggregates.Workout.Cardio;
using A2S.Domain.Services;
using A2S.Tests.Shared.Builders;
using FluentAssertions;
using Xunit;

namespace A2S.Application.Tests.Hevy;

public class ExerciseNoteBuilderTests
{
    [Fact]
    public void Build_CardioExercise_IsTheBlockText()
    {
        var stairs = new ExerciseBuilder().AsCardioStage(CardioLane.Hard).Build();

        ExerciseNoteBuilder.Build(stairs, new A2SProgramProvider().GetWeekParameters(1), [])
            .Should().Be("3 min easy (L3) → 6 min @ level 6 → 2 min easy (L3)");
    }
}
