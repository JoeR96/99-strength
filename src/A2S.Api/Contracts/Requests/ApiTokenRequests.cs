namespace A2S.Api.Contracts.Requests;

/// <summary>Body for <c>POST /api/v1/users/me/api-tokens</c>. The name is 1 to 50 characters.</summary>
public sealed record CreateApiTokenRequest(string Name);
