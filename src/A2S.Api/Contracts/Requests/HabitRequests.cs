using System.Text.Json.Serialization;
using A2S.Application.Commands.Habits;
using A2S.Domain.Aggregates.Habits;

namespace A2S.Api.Contracts.Requests;

/// <summary>Request body for creating a habit.</summary>
public sealed record CreateHabitRequest(
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter<HabitType>))] HabitType Type,
    decimal Target,
    decimal Minimum,
    string? Unit,
    int Order,
    string? Icon);

/// <summary>Request body for updating a habit. The type cannot change.</summary>
public sealed record UpdateHabitRequest(
    string Name,
    decimal Target,
    decimal Minimum,
    string? Unit,
    int Order,
    string? Icon);

/// <summary>Request body for setting a day's value. <paramref name="ClientTimestamp"/> drives last-write-wins.</summary>
public sealed record SetHabitLogRequest(decimal Value, DateTimeOffset ClientTimestamp);

/// <summary>Request body for the +1 / -1 tap.</summary>
public sealed record IncrementHabitRequest(int Delta, DateTimeOffset ClientTimestamp);

/// <summary>Request body for importing habits and their history from another system.</summary>
public sealed record ImportHabitsRequest(IReadOnlyList<ImportHabitDto> Habits);

/// <summary>Request body for a weigh-in.</summary>
public sealed record RecordBodyWeightRequest(decimal Kg);
