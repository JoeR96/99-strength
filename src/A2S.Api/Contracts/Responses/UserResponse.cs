namespace A2S.Api.Contracts.Responses;

/// <summary>
/// Response model for user data.
/// </summary>
public sealed record UserResponse(string Id, string Email, string Name, DateTime CreatedAt);
