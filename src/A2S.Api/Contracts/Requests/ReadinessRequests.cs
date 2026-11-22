using System.Text.Json.Serialization;
using A2S.Domain.Aggregates.Readiness;

namespace A2S.Api.Contracts.Requests;

/// <summary>Body for <c>PUT /api/v1/readiness/{date}</c>.</summary>
public sealed record SetReadinessRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter<ReadinessColour>))] ReadinessColour Colour);

/// <summary>Body for <c>PUT /api/v1/readiness/{date}/finisher</c>: keep the finisher on a red day (true) or skip it (false).</summary>
public sealed record SetReadinessFinisherRequest(bool Keep);
