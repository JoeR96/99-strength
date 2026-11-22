using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Common;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

/// <summary>
/// <c>BackfillStairFinisherSegments</c> moves an existing Stair finisher onto the new defaults (warm-up, cool-down,
/// 5 → 15 reset 10, 6 → 10 rounds reset 8) keeping its position, leaves other tracks alone, and its Down reverts.
/// </summary>
[Collection("Database")]
public class StairFinisherBackfillTests : IAsyncLifetime
{
    private static readonly UserId Owner = new("user_stair_backfill_01");
    private static readonly DateOnly Start = new(2026, 11, 2);
    private static readonly DateTimeOffset At = new(2026, 11, 2, 18, 0, 0, TimeSpan.Zero);

    private readonly DatabaseFixture _fixture;
    private A2SDbContext _db = null!;

    public StairFinisherBackfillTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _db = await MigratedDatabase.CreateAsync(_fixture, "stairbackfill");
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    /// <summary>The Stair finisher as the template shipped it before this change: no segments, 5 → 10, 4 → 8.</summary>
    private static IReadOnlyList<TrackStage> OldStairStages() =>
    [
        TrackStage.Double("Continuous", TrackEffortTarget.Hard,
            new DoubleProgressionSettings(VolumeUnit.Minutes, 5, 10, 1, null, IntensityUnit.Level, 6, 1, 9, 2),
            zoneTargetFraction: 0.5m, note: "After lifting. Steady and hard: zone 4–5."),
        TrackStage.Double("Intervals", TrackEffortTarget.Hard,
            new DoubleProgressionSettings(VolumeUnit.Rounds, 4, 8, 1, null, IntensityUnit.Level, 10, 1, null, 2, 60, 60),
            zoneTargetFraction: 0.5m, note: "1 min hard / 1 min easy. Level is the hard minute.")
    ];

    private static CardioTrack OldStair(string? templateKey = CardioTrackTemplates.StairFinisher) =>
        CardioTrack.Create(Owner, "Stair finisher", CardioModality.StairClimber, OldStairStages(), Start, templateKey: templateKey);

    private async Task<(IMigrator Migrator, string Ours, string Previous)> RollBackAsync()
    {
        var migrator = _db.GetService<IMigrator>();
        var migrations = _db.Database.GetMigrations().ToList();
        var ours = migrations.Single(m => m.EndsWith("_BackfillStairFinisherSegments", StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(ours) - 1];
        await migrator.MigrateAsync(previous);
        return (migrator, ours, previous);
    }

    private async Task<CardioTrack> LoadAsync(CardioTrackId id)
    {
        await using var fresh = MigratedDatabase.Reopen(_db);
        return (await new CardioTrackRepository(fresh).GetByIdAsync(id))!;
    }

    [Fact]
    public async Task Up_AddsSegmentsAndNewRanges_KeepingThePosition_AndDownReverts()
    {
        var (migrator, ours, previous) = await RollBackAsync();

        // In stage one at 8 min and level 7, one qualifying session banked.
        var continuous = OldStair();
        var log = new A2S.Domain.Aggregates.Plan.CardioLog(300, 165, null, null, null, 5, effort: EffortRating.Brutal);
        for (var i = 0; i < 2 * 3 + 2 * 6 + 1; i++)
        {
            var minutes = (int)continuous.State.Volume;
            continuous.LogSession($"s{i}", Start.AddDays(i), log with { DurationSeconds = minutes * 60, HighIntensityMinutes = minutes }, true, null, At);
        }

        // In the intervals at 4 rounds (below the new minimum of 6).
        var intervals = OldStair();
        intervals.UpdateStages(OldStairStages(), At);
        typeof(CardioTrack).GetProperty(nameof(CardioTrack.State))!.SetValue(intervals,
            intervals.State with { StageIndex = 1, Volume = 4, Intensity = 11, Qualifying = 1 });

        var other = OldStair(templateKey: null);

        var repository = new CardioTrackRepository(_db);
        await repository.AddAsync(continuous);
        await repository.AddAsync(intervals);
        await repository.AddAsync(other);
        await _db.SaveChangesAsync();
        var continuousBefore = continuous.State;

        await migrator.MigrateAsync(ours);

        var a = await LoadAsync(continuous.Id);
        a.Stages.Should().AllSatisfy(s =>
        {
            s.WarmUp.Should().Be(new TrackSegment(3, 3, "Z2"));
            s.CoolDown.Should().Be(new TrackSegment(3, 3, "Z1–2"));
        });
        a.Stages[0].DoubleProgression!.VolumeMax.Should().Be(15);
        a.Stages[0].DoubleProgression!.VolumeResetTo.Should().Be(10);
        a.Stages[1].DoubleProgression!.VolumeMin.Should().Be(6);
        a.Stages[1].DoubleProgression!.VolumeMax.Should().Be(10);
        a.Stages[1].DoubleProgression!.VolumeResetTo.Should().Be(8);
        a.State.Should().Be(continuousBefore, "the position (volume, level, qualifying, step) is kept");
        a.Prescription.Minutes.Should().Be((int)continuousBefore.Volume + 6);
        a.LastSnapshot!.Stages[0].DoubleProgression!.VolumeMax.Should().Be(15, "an undo must not bring the old range back");

        var b = await LoadAsync(intervals.Id);
        b.State.StageIndex.Should().Be(1);
        b.State.Volume.Should().Be(6, "4 rounds is clamped into the new 6 → 10 range");
        b.State.Intensity.Should().Be(11);
        b.State.Qualifying.Should().Be(1);

        var c = await LoadAsync(other.Id);
        c.Stages[0].WarmUp.Should().BeNull("only template stair finishers are migrated");
        c.Stages[0].DoubleProgression!.VolumeMax.Should().Be(10);

        await migrator.MigrateAsync(previous);

        var reverted = await LoadAsync(continuous.Id);
        reverted.Stages.Should().AllSatisfy(s => s.WarmUp.Should().BeNull());
        reverted.Stages[0].DoubleProgression!.VolumeMax.Should().Be(10);
        reverted.Stages[0].DoubleProgression!.VolumeResetTo.Should().BeNull();
        reverted.Stages[1].DoubleProgression!.VolumeMin.Should().Be(4);
        reverted.State.Should().Be(continuousBefore);

        await migrator.MigrateAsync();
    }
}
