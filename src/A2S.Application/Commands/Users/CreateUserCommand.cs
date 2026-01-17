using A2S.Application.Common;
using A2S.Application.DTOs;

namespace A2S.Application.Commands.Users;

/// <summary>
/// Command to create a new user.
/// </summary>
public sealed record CreateUserCommand(
    string Email,
    string Name) : ICommand<UserDto>;
