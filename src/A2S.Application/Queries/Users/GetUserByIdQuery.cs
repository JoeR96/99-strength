using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Common;

namespace A2S.Application.Queries.Users;

/// <summary>
/// Query to get a user by their ID.
/// </summary>
public sealed record GetUserByIdQuery(UserId UserId) : IQuery<UserDto?>;
