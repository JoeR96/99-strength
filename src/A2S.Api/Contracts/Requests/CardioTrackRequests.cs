using A2S.Application.Cardio;
using A2S.Application.DTOs;

namespace A2S.Api.Contracts.Requests;

/// <summary>Body for <c>PUT /api/v1/cardio-tracks/{id}</c>.</summary>
public sealed record UpdateCardioTrackRequest(string Name, string? Notes, IReadOnlyList<TrackStageDto> Stages, TrackBenchmarkDto? Benchmark);

/// <summary>Body for <c>POST /api/v1/cardio-tracks/{id}/sessions</c>.</summary>
public sealed record LogCardioTrackSessionRequest(CardioLogDto Log, bool CompletedAsPrescribed = true, DateOnly? Date = null, string? ClientKey = null);

/// <summary>Body for <c>POST /api/v1/cardio-tracks/{id}/suggestion</c>: accept (true) or decline (false).</summary>
public sealed record DecideSuggestionRequest(bool Accept);

/// <summary>Body for <c>POST /api/v1/cardio-tracks/{id}/benchmarks</c>.</summary>
public sealed record RecordBenchmarkRequest(decimal Value, DateOnly? Date = null, string? Notes = null);

/// <summary>Body for <c>PUT /api/v1/cardio-tracks/heart-rate-zones</c>.</summary>
public sealed record SetHeartRateZonesRequest(int? MaxHeartRate, IReadOnlyList<int>? LowerPercents);

/// <summary>Body for <c>PUT /api/v1/cardio-tracks/{id}/gate</c>.</summary>
public sealed record SetCardioTrackGateRequest(IReadOnlyList<GateConditionDto>? Conditions, bool UseDefault = false);
