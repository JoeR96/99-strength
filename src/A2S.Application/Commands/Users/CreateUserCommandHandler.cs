using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Entities;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Users;

/// <summary>
/// Handler for CreateUserCommand.
/// </summary>
public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUserCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<UserDto>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // Check if email already exists
        var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser is not null)
        {
            return Result.Failure<UserDto>(
                $"A user with email '{request.Email}' already exists.",
                ErrorCode.Conflict);
        }

        // Create user via factory method
        var user = User.Create(request.Email, request.Name);

        // Persist user
        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Return DTO
        return Result.Success(new UserDto(
            user.Id.Value,
            user.Email,
            user.Name,
            user.CreatedAt));
    }
}
