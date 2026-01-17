using A2S.Application.DTOs;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.Users;

/// <summary>
/// Handler for GetUserByIdQuery.
/// </summary>
public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto?>
{
    private readonly IUserRepository _userRepository;

    public GetUserByIdQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        return new UserDto(
            user.Id,
            user.Email,
            user.Name,
            user.CreatedAt);
    }
}
