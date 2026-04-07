namespace A2S.Api.Contracts.Responses;

public sealed record HevyWorkoutListResponse
{
    public List<HevyWorkoutSummary> Workouts { get; init; } = [];
    public int Page { get; init; }
    public int PageCount { get; init; }
}

public sealed record HevyWorkoutSummary
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string StartTime { get; init; } = string.Empty;
    public string EndTime { get; init; } = string.Empty;
    public int ExerciseCount { get; init; }
    public List<HevyExerciseSummary> Exercises { get; init; } = [];
}

public sealed record HevyExerciseSummary
{
    public string ExerciseTemplateId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public int SetCount { get; init; }
    public decimal BestWeight { get; init; }
    public int BestReps { get; init; }
    public decimal TotalVolume { get; init; }
}

public sealed record HevyExerciseHistoryResponse
{
    public string ExerciseTemplateId { get; init; } = string.Empty;
    public List<ExerciseSessionPoint> Sessions { get; init; } = [];
    public int TotalSessions { get; init; }
}

public sealed record ExerciseSessionPoint
{
    public string Date { get; init; } = string.Empty;
    public string WorkoutTitle { get; init; } = string.Empty;
    public int Sets { get; init; }
    public decimal MaxWeight { get; init; }
    public int MaxReps { get; init; }
    public decimal TotalVolume { get; init; }
    public double AvgWeight { get; init; }
    public double AvgReps { get; init; }
    public List<SetDetail> SetDetails { get; init; } = [];
}

public sealed record SetDetail
{
    public decimal WeightKg { get; init; }
    public int Reps { get; init; }
    public string Type { get; init; } = "normal";
}
