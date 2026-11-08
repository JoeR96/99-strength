namespace A2S.Application.DTOs;

/// <summary>
/// Weekly volume and session-time picture for one lifting day. Warnings inform; nothing blocks.
/// </summary>
public sealed record DayBudgetDto(
    IReadOnlyDictionary<string, int> HardSetsThisWeek,
    IReadOnlyList<VolumeWarningDto> VolumeWarnings,
    SessionTimeEstimateDto Time);

public sealed record VolumeWarningDto(string Bucket, int Sets, int Cap, string Message);

public sealed record SessionTimeEstimateDto(
    int TotalMinutes,
    int WarmUpMinutes,
    IReadOnlyList<ExerciseTimeDto> PerExercise,
    bool OverBudget,
    int BudgetMinutes);

public sealed record ExerciseTimeDto(string Name, int Minutes);
