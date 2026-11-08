namespace A2S.Domain.Aggregates.Plan;

/// <summary>The weekly allowances a person can spend.</summary>
public enum AllowanceKind
{
    FlexibleMeal,
    Alcohol
}

/// <summary>One use of a weekly allowance on a local date. Immutable value object.</summary>
public sealed record AllowanceUse(DateOnly Date, AllowanceKind Kind);
