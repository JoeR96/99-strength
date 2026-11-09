using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Plan;

/// <summary>A target trend weight on a date ("7 Dec 2026 — 92 kg"). Immutable value object.</summary>
public sealed record Checkpoint
{
    public const decimal MinimumKg = 30m;
    public const decimal MaximumKg = 300m;

    public DateOnly Date { get; }
    public decimal TargetKg { get; }
    public string? Note { get; }

    public Checkpoint(DateOnly date, decimal targetKg, string? note)
    {
        if (targetKg < MinimumKg || targetKg > MaximumKg)
        {
            throw new BusinessRuleViolationException($"Checkpoint target must be between {MinimumKg} and {MaximumKg} kg");
        }

        Date = date;
        TargetKg = targetKg;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
