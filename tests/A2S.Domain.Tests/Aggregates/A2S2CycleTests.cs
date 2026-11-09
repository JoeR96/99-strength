using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Events;
using A2S.Domain.Services;
using A2S.Domain.Tests.TestData;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Aggregates;

/// <summary>
/// Brief § 6 "A2S2 cycle", at the aggregate level: a full 21-week run driven row by row from
/// <c>research/A2S2_Validation_Data.md</c>, deload weeks, custom block sequences, restart after
/// week 21, undo across a week rollover and of the very first completion, double-tap complete,
/// and substitution mid-block. The API-level counterparts live in
/// <c>tests/A2S.Api.Tests/Integration/FullCycleIntegrationTests.cs</c> and friends.
/// </summary>
public class A2S2CycleTests
{
    private const string Ohp = "Overhead Press (Smith Machine)";
    private const string SmithSquat = "Squat (Smith Machine)";
    private static readonly UserId Joe = new("user_a2s2_cycle");

    public static TheoryData<string, int> EveryRow()
    {
        var data = new TheoryData<string, int>();
        foreach (var heading in new[] { A2S2ValidationTable.OverheadPressHeading, A2S2ValidationTable.SmithSquatHeading })
        {
            foreach (var row in A2S2ValidationTable.For(heading))
            {
                data.Add(heading, row.Week);
            }
        }

        return data;
    }

    // ---------------------------------------------------------------------------------------
    // Full 21-week run against the research tables
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ValidationTable_ParsesBothLiftsWithTwentyOneRowsEach()
    {
        A2S2ValidationTable.OverheadPress.Select(r => r.Week).Should().Equal(Enumerable.Range(1, 21));
        A2S2ValidationTable.SmithSquat.Select(r => r.Week).Should().Equal(Enumerable.Range(1, 21));
        A2S2ValidationTable.InitialTm(A2S2ValidationTable.OverheadPressHeading).Should().Be(65m);
        A2S2ValidationTable.InitialTm(A2S2ValidationTable.SmithSquatHeading).Should().Be(110m);

        // The Smith Squat table was built to exercise every multiplier once.
        A2S2ValidationTable.SmithSquat.Select(r => r.Multiplier).Distinct().Should()
            .BeEquivalentTo(new[] { 1.03m, 1.01m, 1.005m, 1m, 1.015m, 1.02m, 0.98m, 0.95m });
    }

    /// <summary>
    /// Replays every week before <paramref name="week"/> with the AMRAP results the research
    /// tables record (above, at and below target, and weeks with nothing entered) and checks the
    /// TM the lift arrives at matches the table's row for that week. The spreadsheet keeps full
    /// precision between weeks while the domain stores the TM to 2 dp, so the value is checked
    /// to ±0.01 kg and the applied multiplier exactly.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryRow))]
    public void FullCycle_TmAtTheStartOfEachWeek_MatchesTheValidationTable(string heading, int week)
    {
        var rows = A2S2ValidationTable.For(heading);
        var liftName = heading == A2S2ValidationTable.OverheadPressHeading ? Ohp : SmithSquat;
        var workout = CreateBriefWorkout();
        var lift = workout.Exercises.Single(e => e.Name == liftName);

        var expectedChain = A2S2ValidationTable.InitialTm(heading);
        for (var w = 1; w < week; w++)
        {
            var row = rows[w - 1];
            var before = lift.GetTrainingMax()!.Value;
            workout.ClearDomainEvents();

            CompleteWeek(workout, w);

            var adjustments = workout.DomainEvents.OfType<TrainingMaxAdjusted>()
                .Where(e => e.ProgressionId == lift.Progression.Id).ToList();
            if (row.Multiplier == 1m)
            {
                adjustments.Should().BeEmpty($"week {w} of {liftName} carries the TM forward");
                lift.GetTrainingMax()!.Value.Should().Be(before);
            }
            else
            {
                adjustments.Should().ContainSingle($"week {w} of {liftName} adjusts the TM once")
                    .Which.Adjustment.Amount.Should().Be(row.Multiplier - 1m, $"week {w} applies ×{row.Multiplier}");
            }

            expectedChain = Math.Round(expectedChain * row.Multiplier, 2);
        }

        workout.CurrentWeek.Should().Be(week);
        var row0 = rows[week - 1];
        var tm = lift.GetTrainingMax()!.Value;
        tm.Should().Be(expectedChain, $"the 2 dp chain of multipliers reaches week {week}");
        tm.Should().BeApproximately(row0.TmAtStart, 0.01m, $"week {week} of the {liftName} table shows {row0.TmAtStart}");
    }

    [Fact]
    public void FullCycle_SmithSquatRepOutTargets_MatchTheTableEveryWorkingWeek()
    {
        var workout = CreateBriefWorkout();
        var squat = workout.Exercises.Single(e => e.Name == SmithSquat);

        foreach (var row in A2S2ValidationTable.SmithSquat)
        {
            var planned = workout.GetPlannedSetsForExercise(squat.Id).ToList();
            var amrap = planned.SingleOrDefault(s => s.IsAmrap);

            if (row.IsDeload)
            {
                amrap.Should().BeNull($"week {row.Week} is a deload with no rep-out");
                planned.Should().HaveCount(A2SHypertrophyProgram.DeloadSets);
                planned.Should().OnlyContain(s => s.TargetReps == A2SHypertrophyProgram.DeloadReps);
            }
            else if (row.RepOutTarget is { } target)
            {
                amrap.Should().NotBeNull($"week {row.Week} has a rep-out set");
                amrap!.TargetReps.Should().Be(target, $"week {row.Week} rep-out target");
                planned.Should().HaveCount(A2SHypertrophyProgram.WorkingSets);
            }
            // Weeks the table leaves as "..." (15–20) have no row-level target to check.

            CompleteWeek(workout, row.Week);
        }
    }

    [Fact]
    public void FullCycle_AfterWeek21_ProgramIsCompletedWithTheTablesFinalTms()
    {
        var workout = CreateBriefWorkout();

        for (var week = 1; week <= 21; week++)
        {
            CompleteWeek(workout, week);
        }

        workout.Status.Should().Be(WorkoutStatus.Completed);
        workout.CompletedAt.Should().NotBeNull();
        workout.CurrentWeek.Should().Be(21);
        workout.CompletedActivities.Should().HaveCount(84, "21 weeks × 4 days");
        workout.DomainEvents.OfType<WorkoutCompleted>().Should().ContainSingle();
        Tm(workout, Ohp).Should().BeApproximately(A2S2ValidationTable.OverheadPress[20].TmAtStart, 0.01m);
        Tm(workout, SmithSquat).Should().BeApproximately(A2S2ValidationTable.SmithSquat[20].TmAtStart, 0.01m);
    }

    // ---------------------------------------------------------------------------------------
    // Deload weeks 7 / 14 / 21
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(21)]
    public void DeloadWeek_HasFourSetsOfFiveWithNoAmrap_AndAHighRepOutDoesNotMoveTheTm(int deloadWeek)
    {
        var workout = CreateBriefWorkout();
        RunToWeek(workout, deloadWeek);
        var squat = workout.Exercises.Single(e => e.Name == SmithSquat);
        var tmBefore = squat.GetTrainingMax()!.Value;

        workout.IsDeloadWeek().Should().BeTrue();
        var planned = workout.GetPlannedSetsForExercise(squat.Id).ToList();
        planned.Should().HaveCount(4);
        planned.Should().OnlyContain(s => s.TargetReps == 5 && !s.IsAmrap);
        planned.Should().OnlyContain(s => s.Weight.Value == planned[0].Weight.Value, "every deload set is at 58 %");

        workout.ClearDomainEvents();
        // The lifter still flags the last set as a rep-out and does 15 reps.
        CompleteDay(workout, DayNumber.Day2, delta: 10, flagLastSetAsAmrap: true);

        squat.GetTrainingMax()!.Value.Should().Be(tmBefore, "deload weeks carry the TM forward whatever was lifted");
        workout.DomainEvents.OfType<TrainingMaxAdjusted>().Should().BeEmpty();
        workout.DomainEvents.OfType<DayCompleted>().Should().ContainSingle();
    }

    [Fact]
    public void WeekProgressedEvents_FlagExactlyWeeks7And14And21AsDeloads()
    {
        var workout = CreateBriefWorkout();
        var deloadWeeksEntered = new List<int>();

        for (var week = 1; week <= 21; week++)
        {
            workout.ClearDomainEvents();
            CompleteWeek(workout, week);
            deloadWeeksEntered.AddRange(workout.DomainEvents.OfType<WeekProgressed>()
                .Where(e => e.IsDeloadWeek).Select(e => e.NewWeek));
        }

        deloadWeeksEntered.Should().Equal(7, 14, 21);
    }

    // ---------------------------------------------------------------------------------------
    // Custom block sequences
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void CustomBlockSequence_RepeatingBlockOne_RunsTwentyEightWeeks_WithDeloadsEverySeventhWeek()
    {
        var workout = CreateBriefWorkout(blockSequence: [1, 1, 2, 3]);
        var squat = workout.Exercises.Single(e => e.Name == SmithSquat);
        workout.TotalWeeks.Should().Be(28);

        var deloads = new List<int>();
        var repOutByWeek = new Dictionary<int, int?>();
        for (var week = 1; week <= 28; week++)
        {
            workout.CurrentWeek.Should().Be(week);
            repOutByWeek[week] = workout.GetPlannedSetsForExercise(squat.Id).SingleOrDefault(s => s.IsAmrap)?.TargetReps;
            if (workout.IsDeloadWeek())
            {
                deloads.Add(week);
            }

            CompleteWeekOnTarget(workout, week);
        }

        deloads.Should().Equal(7, 14, 21, 28);
        workout.Status.Should().Be(WorkoutStatus.Completed);

        // Weeks 8–14 repeat block 1: same rep-out targets as weeks 1–7.
        for (var week = 1; week <= 7; week++)
        {
            repOutByWeek[week + 7].Should().Be(repOutByWeek[week], $"week {week + 7} repeats block-1 week {week}");
            workout.GetTemplateWeek(week + 7).Should().Be(week);
        }

        // Weeks 15–21 are block 2 (template 8–14) and 22–28 block 3 (template 15–21).
        workout.GetTemplateWeek(15).Should().Be(8);
        workout.GetTemplateWeek(28).Should().Be(21);
        workout.GetBlockType(22).Should().Be(3);
    }

    [Fact]
    public void CustomBlockSequence_ExtendedMidProgram_KeepsTheCurrentWeekAndRecomputesTheBlock()
    {
        var workout = CreateBriefWorkout();
        RunToWeek(workout, 9);
        workout.CurrentBlock.Should().Be(2);

        workout.UpdateBlockSequence([1, 1, 2, 3]);

        workout.TotalWeeks.Should().Be(28);
        workout.CurrentWeek.Should().Be(9);
        workout.CurrentBlock.Should().Be(1, "week 9 now falls in the repeated block 1");
        workout.GetTemplateWeek(9).Should().Be(2);
    }

    // ---------------------------------------------------------------------------------------
    // Restart after week 21
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void RestartAfterWeek21_CarriesTheTmsForward_ArchivesHistory_AndPlansWeekOneFromTheNewTm()
    {
        var workout = CreateBriefWorkout();
        for (var week = 1; week <= 21; week++)
        {
            CompleteWeek(workout, week);
        }

        var finalOhpTm = Tm(workout, Ohp);
        var finalSquatTm = Tm(workout, SmithSquat);
        workout.ClearDomainEvents();

        workout.UpdateBlockSequence([1, 2, 3]);

        workout.Status.Should().Be(WorkoutStatus.Active);
        workout.CurrentWeek.Should().Be(1);
        workout.CurrentDay.Should().Be(1);
        workout.CurrentBlock.Should().Be(1);
        workout.CompletedAt.Should().BeNull();
        workout.CompletedActivities.Should().BeEmpty();
        workout.ArchivedActivities.Should().HaveCount(84);
        workout.DomainEvents.OfType<ProgramRestarted>().Should().ContainSingle().Which.ArchivedActivitiesCount.Should().Be(84);
        Tm(workout, Ohp).Should().Be(finalOhpTm, "a restart never resets the TM");
        Tm(workout, SmithSquat).Should().Be(finalSquatTm);

        var squat = workout.Exercises.Single(e => e.Name == SmithSquat);
        var weekOne = workout.GetPlannedSetsForExercise(squat.Id).ToList();
        weekOne.Should().HaveCount(5);
        weekOne.Single(s => s.IsAmrap).TargetReps.Should().Be(10, "week 1 of block 1 again");
        weekOne[0].Weight.Should().Be(squat.GetTrainingMax()!.CalculateWorkingWeight(A2SHypertrophyProgram.GetIntensity(1)));

        // And the new cycle progresses from the carried-forward TM.
        CompleteDay(workout, DayNumber.Day2, delta: +5);
        Tm(workout, SmithSquat).Should().Be(Math.Round(finalSquatTm * 1.03m, 2));
    }

    // ---------------------------------------------------------------------------------------
    // Undo
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void UndoAfterWeekRollover_RestoresWeekDayAndTm_AndRedoRollsForwardAgain()
    {
        var workout = CreateBriefWorkout();
        var lowerB = workout.Exercises.Single(e => e.AssignedDay == DayNumber.Day4);
        var tmBeforeDay4 = lowerB.GetTrainingMax()!.Value;

        CompleteDay(workout, DayNumber.Day1, +5);
        CompleteDay(workout, DayNumber.Day2, +5);
        CompleteDay(workout, DayNumber.Day3, +5);
        CompleteDay(workout, DayNumber.Day4, +5);
        workout.CurrentWeek.Should().Be(2);
        var tmAfterDay4 = lowerB.GetTrainingMax()!.Value;
        tmAfterDay4.Should().Be(Math.Round(tmBeforeDay4 * 1.03m, 2));
        workout.ClearDomainEvents();

        var result = workout.UndoLastCompletion();

        result.WeekRolledBack.Should().BeTrue();
        result.Day.Should().Be(DayNumber.Day4);
        result.WeekNumber.Should().Be(1);
        workout.CurrentWeek.Should().Be(1);
        workout.CurrentDay.Should().Be(4);
        workout.CurrentBlock.Should().Be(1);
        workout.CompletedActivities.Should().HaveCount(3);
        workout.IsDayCompletedInCurrentWeek(DayNumber.Day4).Should().BeFalse();
        lowerB.GetTrainingMax()!.Value.Should().Be(tmBeforeDay4, "the snapshot taken before day 4 is restored");
        workout.DomainEvents.OfType<CompletionUndone>().Should().ContainSingle();
        workout.AuditEntries.Should().ContainSingle(a => a.Type == AuditEntryType.UndoCompletion);

        // Day 4 is planned on week-1 parameters again and completing it rolls the week forward once more.
        workout.GetPlannedSetsForExercise(lowerB.Id).Single(s => s.IsAmrap).TargetReps.Should().Be(10);
        CompleteDay(workout, DayNumber.Day4, +5);
        workout.CurrentWeek.Should().Be(2);
        workout.CurrentDay.Should().Be(1);
        lowerB.GetTrainingMax()!.Value.Should().Be(tmAfterDay4);
        workout.CompletedActivities.Should().HaveCount(4);
    }

    [Fact]
    public void UndoTheFirstEverCompletion_LeavesAFreshWeekOne_AndASecondUndoIsRefused()
    {
        var workout = CreateBriefWorkout();
        var ohp = workout.Exercises.Single(e => e.Name == Ohp);

        CompleteDay(workout, DayNumber.Day1, +5);
        Tm(workout, Ohp).Should().Be(66.95m);
        workout.CurrentDay.Should().Be(2);

        var result = workout.UndoLastCompletion();

        result.WeekRolledBack.Should().BeFalse();
        result.Day.Should().Be(DayNumber.Day1);
        workout.CurrentWeek.Should().Be(1);
        workout.CurrentDay.Should().Be(1);
        workout.Status.Should().Be(WorkoutStatus.Active);
        workout.CompletedActivities.Should().BeEmpty();
        Tm(workout, Ohp).Should().Be(65m);
        workout.GetPlannedSetsForExercise(ohp.Id).First().Weight.Value.Should().Be(
            TrainingMax.Create(65m, WeightUnit.Kilograms).CalculateWorkingWeight(A2SHypertrophyProgram.GetIntensity(1)).Value);

        var secondUndo = () => workout.UndoLastCompletion();
        secondUndo.Should().Throw<DomainException>().WithMessage("*No completed activities to undo*");
    }

    [Fact]
    public void Undo_RestoresOnlyTheLastCompletion_WhenOtherExercisesMovedOnTheSameWeek()
    {
        var workout = CreateBriefWorkout();
        CompleteDay(workout, DayNumber.Day1, +5); // OHP → 66.95
        CompleteDay(workout, DayNumber.Day2, +5); // Squat → 113.30

        workout.UndoLastCompletion();

        Tm(workout, SmithSquat).Should().Be(110m, "day 2 was undone");
        Tm(workout, Ohp).Should().Be(66.95m, "day 1 stands");
        workout.CurrentDay.Should().Be(2);
    }

    // ---------------------------------------------------------------------------------------
    // Double-tap complete
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void DoubleTapComplete_SecondTapIsRejected_AndProgressionAppliesExactlyOnce()
    {
        var workout = CreateBriefWorkout();
        var ohp = workout.Exercises.Single(e => e.Name == Ohp);
        var plannedBeforeFirstTap = workout.GetPlannedSetsForExercise(ohp.Id).ToList();

        CompleteDay(workout, DayNumber.Day1, +5, plannedBeforeFirstTap);
        var tmAfterFirstTap = Tm(workout, Ohp);
        var eventsAfterFirstTap = workout.DomainEvents.Count;

        // The second tap re-sends the same payload the first one did.
        var secondTap = () => CompleteDay(workout, DayNumber.Day1, +5, plannedBeforeFirstTap);

        secondTap.Should().Throw<DomainException>().WithMessage("*Day1 has already been completed in Week 1*");
        Tm(workout, Ohp).Should().Be(tmAfterFirstTap, "the TM is adjusted once, not twice");
        workout.CompletedActivities.Should().ContainSingle();
        workout.CurrentDay.Should().Be(2);
        workout.CurrentWeek.Should().Be(1);
        workout.DomainEvents.Should().HaveCount(eventsAfterFirstTap, "a rejected tap raises nothing");
    }

    // ---------------------------------------------------------------------------------------
    // Substitution mid-block
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void PermanentSubstitutionMidBlock_KeepsTheTm_ContinuesTheChain_AndTheHistoryReplaysToTheSameTm()
    {
        var workout = CreateBriefWorkout();
        var squat = workout.Exercises.Single(e => e.Name == SmithSquat);
        CompleteWeek(workout, 1); // +5 → 113.30
        CompleteWeek(workout, 2); // +2 → 114.43
        var tmBeforeSwap = Tm(workout, SmithSquat);

        var originalName = workout.SubstituteExercise(squat.Id, "Squat (Barbell)", "barbell-squat");

        originalName.Should().Be(SmithSquat);
        squat.Name.Should().Be("Squat (Barbell)");
        squat.ExternalTemplateId.Should().Be("barbell-squat");
        squat.GetTrainingMax()!.Value.Should().Be(tmBeforeSwap, "a permanent substitution keeps the progression state");
        workout.GetPlannedSetsForExercise(squat.Id).Single(s => s.IsAmrap).TargetReps.Should().Be(6, "week 3 continues from the week-3 formula");

        CompleteWeek(workout, 3); // +2 → 115.57

        Tm(workout, "Squat (Barbell)").Should().Be(Math.Round(tmBeforeSwap * 1.01m, 2));
        workout.CompletedActivities.Should().HaveCount(12);
        workout.CompletedActivities.Where(a => a.Day == DayNumber.Day2).Should().AllSatisfy(a =>
            a.Performances.Should().ContainSingle(p => p.ExerciseId == squat.Id), "history stays attached to the same exercise id");
        workout.CompletedActivities.First(a => a.Day == DayNumber.Day2 && a.WeekNumber == 1)
            .ProgressionSnapshots.Single(s => s.ExerciseId == squat.Id).ExerciseName.Should().Be(SmithSquat, "old snapshots keep the name used at the time");

        // Replaying the recorded history from the original TM lands on the live TM: nothing was lost in the swap.
        var replay = workout.RetrofixTrainingMaxHistory(squat.Id, 110m);
        replay.Should().HaveCount(3);
        replay.Select(c => c.NewTm).Should().Equal(110m, 113.30m, 114.43m);
        Tm(workout, "Squat (Barbell)").Should().Be(115.57m);
    }

    [Fact]
    public void TemporarySubstitutionMidBlock_FreezesTheTmForThatWeekOnly_AndTheChainResumesAfter()
    {
        var workout = CreateBriefWorkout();
        var squat = workout.Exercises.Single(e => e.Name == SmithSquat);
        CompleteWeek(workout, 1); // 113.30
        workout.ClearDomainEvents();

        // Week 2: a leg press stands in for the squat; the lifter does far more than target.
        CompleteDay(workout, DayNumber.Day1, +2);
        CompleteDay(workout, DayNumber.Day2, +8, skipProgression: true);
        CompleteDay(workout, DayNumber.Day3, 0);
        CompleteDay(workout, DayNumber.Day4, 0);

        Tm(workout, SmithSquat).Should().Be(113.30m, "a temporary substitution skips progression");
        workout.DomainEvents.OfType<ProgressionSkipped>().Should().ContainSingle().Which.ExerciseName.Should().Be(SmithSquat);
        workout.AuditEntries.Should().ContainSingle(a => a.Type == AuditEntryType.TemporarySubstitution && a.WeekNumber == 2);
        workout.CurrentWeek.Should().Be(3);

        CompleteWeek(workout, 3); // +2 on 113.30

        Tm(workout, SmithSquat).Should().Be(114.43m, "week 3 progresses from the frozen TM with the week-3 target");
        workout.CompletedActivities.Count(a => a.Performances.Any(p => p.ExerciseId == squat.Id && p.SkipProgression)).Should().Be(1);
    }

    // ---------------------------------------------------------------------------------------
    // Pause
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void PausedWorkout_RefusesCompletions_AndResumesOnTheSameWeekAndDay()
    {
        var workout = CreateBriefWorkout();
        CompleteWeek(workout, 1);
        CompleteDay(workout, DayNumber.Day1, 0);

        workout.Pause();

        workout.Status.Should().Be(WorkoutStatus.Paused);
        var attempt = () => CompleteDay(workout, DayNumber.Day2, 0);
        attempt.Should().Throw<DomainException>().WithMessage("*not active*");
        workout.CurrentWeek.Should().Be(2);
        workout.CurrentDay.Should().Be(2);

        workout.Resume();

        CompleteDay(workout, DayNumber.Day2, 0);
        workout.CurrentWeek.Should().Be(2);
        workout.CurrentDay.Should().Be(3);
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// The brief's four lifting days with the research tables' two lifts on days 1 and 2
    /// (OHP TM 65, Smith Squat TM 110) and two plain A2S2 lifts on days 3 and 4.
    /// </summary>
    private static Workout CreateBriefWorkout(List<int>? blockSequence = null)
    {
        var exercises = new[]
        {
            Linear(Ohp, DayNumber.Day1, 65m),
            Linear(SmithSquat, DayNumber.Day2, 110m),
            Linear("Bench Press (Barbell)", DayNumber.Day3, 100m),
            Linear("Deadlift (Barbell)", DayNumber.Day4, 140m)
        };

        var workout = Workout.Create(Joe, "Cut to 85 — Nov 2026", ProgramVariant.FourDay, exercises, blockSequence);
        workout.Start();
        return workout;
    }

    private static Exercise Linear(string name, DayNumber day, decimal tm) =>
        Exercise.CreateWithLinearProgression(
            name, ExerciseCategory.MainLift, EquipmentType.Barbell, day, 1,
            name.ToLowerInvariant().Replace(' ', '-'),
            TrainingMax.Create(tm, WeightUnit.Kilograms), useAmrap: true, baseSetsPerExercise: 5);

    private static decimal Tm(Workout workout, string exerciseName) =>
        workout.Exercises.Single(e => e.Name == exerciseName).GetTrainingMax()!.Value;

    private static void RunToWeek(Workout workout, int week)
    {
        while (workout.CurrentWeek < week)
        {
            CompleteWeek(workout, workout.CurrentWeek);
        }
    }

    /// <summary>
    /// Completes all four days of <paramref name="week"/>: OHP and Smith Squat with the AMRAP
    /// result from their research rows (or no rep-out when the row has none), days 3 and 4 on target.
    /// </summary>
    private static void CompleteWeek(Workout workout, int week)
    {
        workout.CurrentWeek.Should().Be(week);
        var ohpRow = A2S2ValidationTable.OverheadPress[week - 1];
        var squatRow = A2S2ValidationTable.SmithSquat[week - 1];

        CompleteDay(workout, DayNumber.Day1, ohpRow.Delta ?? 0, flagLastSetAsAmrap: ohpRow.Delta.HasValue);
        CompleteDay(workout, DayNumber.Day2, squatRow.Delta ?? 0, flagLastSetAsAmrap: squatRow.Delta.HasValue);
        CompleteDay(workout, DayNumber.Day3, 0);
        CompleteDay(workout, DayNumber.Day4, 0);
    }

    /// <summary>Completes all four days exactly on target (used past week 21 of longer sequences).</summary>
    private static void CompleteWeekOnTarget(Workout workout, int week)
    {
        workout.CurrentWeek.Should().Be(week);
        foreach (var day in new[] { DayNumber.Day1, DayNumber.Day2, DayNumber.Day3, DayNumber.Day4 })
        {
            CompleteDay(workout, day, 0);
        }
    }

    /// <summary>
    /// Completes a day doing every planned set as prescribed, with the rep-out set at
    /// target + <paramref name="delta"/>. On a deload week the "last set" is only flagged as a
    /// rep-out when asked, to mimic a lifter who taps AMRAP anyway.
    /// </summary>
    private static void CompleteDay(
        Workout workout,
        DayNumber day,
        int delta,
        IReadOnlyList<PlannedSet>? plannedOverride = null,
        bool flagLastSetAsAmrap = true,
        bool skipProgression = false)
    {
        var exercise = workout.GetExercisesForDay(day).Single();
        var planned = plannedOverride?.ToList() ?? workout.GetPlannedSetsForExercise(exercise.Id).ToList();
        var hasPlannedAmrap = planned.Any(s => s.IsAmrap);

        var completed = planned.Select(set =>
        {
            var isLast = set.SetNumber == planned.Count;
            var isRepOut = hasPlannedAmrap ? set.IsAmrap : isLast && flagLastSetAsAmrap;
            var reps = isRepOut ? Math.Max(1, set.TargetReps + delta) : set.TargetReps;
            var wasAmrap = isRepOut && flagLastSetAsAmrap;
            return new CompletedSet(set.SetNumber, set.Weight, reps, wasAmrap);
        }).ToList();

        var performance = new ExercisePerformance(exercise.Id, planned, completed, skipProgression: skipProgression);
        workout.CompleteDay(day, [performance]);
    }
}
