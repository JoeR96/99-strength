using A2S.Application.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Users;

/// <summary>The user's app preferences shown in Settings.</summary>
public sealed record UserPreferencesDto(bool PushRoutinesToHevy);

/// <summary>Reads the current user's preferences.</summary>
public sealed record GetUserPreferencesQuery : IQuery<Result<UserPreferencesDto>>;

/// <summary>Turns "Push routines into Hevy" on or off. Finished sessions are still posted to Hevy.</summary>
public sealed record SetUserPreferencesCommand(bool PushRoutinesToHevy) : ICommand<Result<UserPreferencesDto>>;

public sealed class GetUserPreferencesQueryHandler : IRequestHandler<GetUserPreferencesQuery, Result<UserPreferencesDto>>
{
    private readonly IUserRepository _users;
    private readonly ICurrentUserService _currentUser;

    public GetUserPreferencesQueryHandler(IUserRepository users, ICurrentUserService currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    public async Task<Result<UserPreferencesDto>> Handle(GetUserPreferencesQuery request, CancellationToken cancellationToken)
    {
        var userId = PlanAccess.RequireUser(_currentUser);
        if (userId.IsFailure)
        {
            return PlanAccess.Fail<UserPreferencesDto>(userId);
        }

        var user = await _users.GetByIdAsync(userId.Value, cancellationToken);
        return user is null
            ? Result.Failure<UserPreferencesDto>("User not found.", ErrorCode.NotFound)
            : Result.Success(new UserPreferencesDto(user.PushRoutinesToHevy));
    }
}

public sealed class SetUserPreferencesCommandHandler : IRequestHandler<SetUserPreferencesCommand, Result<UserPreferencesDto>>
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public SetUserPreferencesCommandHandler(IUserRepository users, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<UserPreferencesDto>> Handle(SetUserPreferencesCommand request, CancellationToken cancellationToken)
    {
        var userId = PlanAccess.RequireUser(_currentUser);
        if (userId.IsFailure)
        {
            return PlanAccess.Fail<UserPreferencesDto>(userId);
        }

        var user = await _users.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserPreferencesDto>("User not found.", ErrorCode.NotFound);
        }

        user.SetPushRoutinesToHevy(request.PushRoutinesToHevy);
        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new UserPreferencesDto(user.PushRoutinesToHevy));
    }
}
