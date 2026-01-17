using A2S.Application.Common;
using A2S.Application.DTOs;

namespace A2S.Application.Queries.Users;

/// <summary>
/// Query to get a user by their ID.
/// </summary>
public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserDto?>;
