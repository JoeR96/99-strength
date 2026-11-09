using A2S.Domain.Common;

namespace A2S.Domain.Aggregates.Plan;

/// <summary>How many flexible meals and alcohol occasions the plan allows per Mon–Sun week.</summary>
public sealed record WeeklyAllowances
{
    public int FlexibleMeals { get; }
    public int AlcoholOccasions { get; }

    public WeeklyAllowances(int flexibleMeals = 1, int alcoholOccasions = 1)
    {
        if (flexibleMeals < 0 || alcoholOccasions < 0)
        {
            throw new BusinessRuleViolationException("Weekly allowances cannot be negative");
        }

        FlexibleMeals = flexibleMeals;
        AlcoholOccasions = alcoholOccasions;
    }

    public static WeeklyAllowances Default => new();
}
