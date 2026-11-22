using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Aggregates.User;
using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Entities;

public class UserHeartRateTests
{
    private static User NewUser() => User.Create("hr@example.com", "Heart", new UserId("user_hr_0001"), new DateTime(2026, 11, 1));

    [Fact]
    public void NoMaxHeartRate_HasNoZones()
    {
        NewUser().HeartRateZones.Should().BeNull();
    }

    [Fact]
    public void SetHeartRate_DefaultsToWhoopZones()
    {
        var user = NewUser();

        user.SetHeartRate(190, null);

        user.HeartRateZones!.LowerPercents.Should().Equal(50, 60, 70, 80, 90);
        user.HeartRateZones.UpperBpm(2).Should().Be(133);
        user.HeartRateZones.UpperBpm(5).Should().Be(190);
        user.HeartRateZones.LowerBpm(4).Should().Be(152);
    }

    [Fact]
    public void SetHeartRate_AcceptsEditedZones_AndRejectsNonRising()
    {
        var user = NewUser();
        user.SetHeartRate(200, [55, 65, 75, 85, 92]);
        user.HeartRateZones!.UpperBpm(2).Should().Be(150);

        var act = () => user.SetHeartRate(200, [50, 70, 60, 80, 90]);
        act.Should().Throw<BusinessRuleViolationException>();

        user.SetHeartRate(null, null);
        user.MaxHeartRate.Should().BeNull();
    }
}

public class CardioLogExtensionTests
{
    [Fact]
    public void ExistingShape_StillValid_WithNewFieldsDefaulted()
    {
        var log = new CardioLog(1800, 140, 6, 3000, "ok", 10);

        log.Pain.Should().Be(PainFlag.None);
        log.Source.Should().Be(CardioLogSource.Manual);
        log.Effort.Should().BeNull();
        log.EffectiveEffort.Should().Be(EffortRating.Solid);
        log.EffectiveRpe.Should().Be(6);
    }

    [Fact]
    public void EffortButton_StoresRpe479()
    {
        new CardioLog(600, null, null, null, null, effort: EffortRating.Easy).EffectiveRpe.Should().Be(4);
        new CardioLog(600, null, null, null, null, effort: EffortRating.Solid).EffectiveRpe.Should().Be(7);
        new CardioLog(600, null, null, null, null, effort: EffortRating.Brutal).EffectiveRpe.Should().Be(9);
    }

    [Fact]
    public void NewFields_AreValidated()
    {
        var zone5 = () => new CardioLog(600, null, null, null, null, zone5Minutes: 11);
        zone5.Should().Throw<BusinessRuleViolationException>();

        var floors = () => new CardioLog(600, null, null, null, null, floors: -1);
        floors.Should().Throw<BusinessRuleViolationException>();

        var log = new CardioLog(600, null, null, null, null, intensityUnit: IntensityUnit.Level, intensityValue: 9,
            floors: 40, pain: PainFlag.Niggle, painArea: " left knee ", source: CardioLogSource.Hevy);
        log.PainArea.Should().Be("left knee");
        log.IntensityUnit.Should().Be(IntensityUnit.Level);
    }
}
