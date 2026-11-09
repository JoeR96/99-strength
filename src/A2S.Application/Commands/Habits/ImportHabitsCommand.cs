using System.Text.Json.Serialization;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Habits;
using FluentValidation;

namespace A2S.Application.Commands.Habits;

/// <summary>One habit from an external source (e.g. Terran CC) with its history.</summary>
public sealed record ImportHabitDto(
    string ExternalKey,
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter<HabitType>))] HabitType Type,
    decimal Target,
    decimal Minimum,
    string? Unit,
    int Order,
    IReadOnlyList<ImportHabitLogDto> Logs);

public sealed record ImportHabitLogDto(DateOnly Date, decimal Value);

/// <summary>What an import did. Running the same import twice gives zero written logs the second time.</summary>
public sealed record ImportResult(int HabitsCreated, int HabitsMatched, int LogsWritten, int LogsUnchanged);

/// <summary>
/// Idempotent import: habits are matched by <see cref="ImportHabitDto.ExternalKey"/> and created when missing;
/// logs are written only when the stored value differs.
/// </summary>
public sealed record ImportHabitsCommand(IReadOnlyList<ImportHabitDto> Habits) : ICommand<Result<ImportResult>>;

public sealed class ImportHabitsCommandValidator : AbstractValidator<ImportHabitsCommand>
{
    public ImportHabitsCommandValidator()
    {
        RuleFor(x => x.Habits).NotNull();
        RuleForEach(x => x.Habits).ChildRules(habit =>
        {
            habit.RuleFor(h => h.ExternalKey).NotEmpty().MaximumLength(100);
            habit.RuleFor(h => h.Name).NotEmpty().MaximumLength(100);
            habit.RuleFor(h => h.Type).IsInEnum();
            habit.RuleFor(h => h.Logs).NotNull();
            habit.RuleForEach(h => h.Logs).ChildRules(log => log.RuleFor(l => l.Value).GreaterThanOrEqualTo(0));
        });
    }
}
