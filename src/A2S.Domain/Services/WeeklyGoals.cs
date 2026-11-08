namespace A2S.Domain.Services;

/// <summary>
/// Progress for one Mon–Sun week: sessions planned (excluding paused days) and done, and the
/// allowances used against what the plan permits. Used/allowed may exceed allowed; the UI shows the overflow.
/// </summary>
public sealed record WeeklyGoals(
    DateOnly WeekStart,
    int LiftsPlanned,
    int LiftsDone,
    int CardioPlanned,
    int CardioDone,
    int MobilityPlanned,
    int MobilityDone,
    int FlexibleMealsUsed,
    int FlexibleMealsAllowed,
    int AlcoholOccasions,
    int AlcoholAllowed);
