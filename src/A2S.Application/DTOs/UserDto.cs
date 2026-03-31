namespace A2S.Application.DTOs;

/// <summary>
/// Data transfer object for User entity.
/// </summary>
public sealed record UserDto(
    string Id,
    string Email,
    string Name,
    DateTime CreatedAt);
