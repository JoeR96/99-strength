using A2S.Application.DTOs;
using A2S.Domain.Entities;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Users;

/// <summary>
/// Handler for CreateUserCommand.
/// </summary>
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserDto>
{
    private readonly IUserRepository _userRepository;

    public CreateUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // Check if email already exists
        var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser is not null)
        {
            throw new InvalidOperationException($"A user with email '{request.Email}' already exists.");
        }

        // Create user via factory method
        var user = User.Create(request.Email, request.Name);

        // Persist user
        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        // Return DTO
        return new UserDto(
            user.Id,
            user.Email,
            user.Name,
            user.CreatedAt);
    }
}
