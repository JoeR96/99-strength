using System.Text.Json.Serialization;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Services;

namespace A2S.Application.DTOs;

/// <summary>A habit definition as the client sees it.</summary>
public sealed record HabitDto(
    Guid Id,
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter<HabitType>))] HabitType Type,
    decimal Target,
    decimal Minimum,
    string? Unit,
    int Order,
    bool Archived,
    string? Icon,
    string? ExternalKey)
{
    public static HabitDto From(HabitDefinition habit) => new(
        habit.Id.Value,
        habit.Name,
        habit.Type,
        habit.Target,
        habit.Minimum,
        habit.Unit,
        habit.Order,
        habit.Archived,
        habit.Icon,
        habit.ExternalKey);
}

/// <summary>One habit's state for one day: definition, logged value and whether it met minimum / target.</summary>
public sealed record HabitTodayDto(
    Guid Id,
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter<HabitType>))] HabitType Type,
    decimal Target,
    decimal Minimum,
    string? Unit,
    decimal Value,
    bool MetMinimum,
    bool MetTarget,
    int Order,
    string? Icon);

/// <summary>The streak as the client sees it.</summary>
public sealed record HabitStreakDto(int Current, int Best, DateOnly? LastCountedDay, bool TodayCounted, bool GraceUsed)
{
    public static HabitStreakDto From(HabitStreak streak) =>
        new(streak.Current, streak.Best, streak.LastCountedDay, streak.TodayCounted, streak.GraceUsed);
}

/// <summary>The habit screen for one local date.</summary>
public sealed record HabitsTodayDto(DateOnly Date, IReadOnlyList<HabitTodayDto> Habits, HabitStreakDto Streak);

/// <summary>A stored log value, returned after every write so the client can reconcile.</summary>
public sealed record HabitLogDto(Guid HabitId, DateOnly Date, decimal Value, DateTimeOffset UpdatedAtUtc)
{
    public static HabitLogDto From(HabitLog log) => new(log.HabitId.Value, log.Date, log.Value, log.UpdatedAtUtc);
}

/// <summary>One habit's value on one day within a history range.</summary>
public sealed record HabitHistoryEntryDto(Guid HabitId, DateOnly Date, decimal Value, bool MetMinimum, bool MetTarget);

/// <summary>Per-habit, per-day values across a date range, with the definitions they belong to.</summary>
public sealed record HabitHistoryDto(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<HabitDto> Habits,
    IReadOnlyList<HabitHistoryEntryDto> Entries);
