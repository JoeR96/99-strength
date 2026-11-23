namespace A2S.Api.Contracts.Requests;

/// <summary>One phase change: absolute UTC time and the notification text ("Hard: level 6 for 1 min").</summary>
public sealed record FinisherNudgeRequest(DateTimeOffset At, string Body);

/// <summary>Request body for <c>POST /api/v1/finisher-sessions/{clientSessionId}/schedule</c>; replaces any earlier schedule.</summary>
public sealed record ReplaceFinisherScheduleRequest(int Day, string? Title, IReadOnlyList<FinisherNudgeRequest>? Nudges);
