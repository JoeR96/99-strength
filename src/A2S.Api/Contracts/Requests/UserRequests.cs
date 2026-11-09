namespace A2S.Api.Contracts.Requests;

/// <summary>
/// Request body for creating a user.
/// </summary>
public sealed record CreateUserRequest(string Email, string Name);
