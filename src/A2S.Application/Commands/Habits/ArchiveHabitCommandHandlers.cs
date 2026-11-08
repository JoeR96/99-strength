using A2S.Application.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Habits;

public sealed class ArchiveHabitCommandHandler : IRequestHandler<ArchiveHabitCommand, Result>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public ArchiveHabitCommandHandler(IHabitRepository habits, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(ArchiveHabitCommand request, CancellationToken cancellationToken)
    {
        var user = HabitAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return user;
        }

        var owned = await HabitAccess.LoadOwnedAsync(_habits, user.Value, request.HabitId, cancellationToken);
        if (owned.IsFailure)
        {
            return owned;
        }

        owned.Value.Archive();
        _habits.Update(owned.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class UnarchiveHabitCommandHandler : IRequestHandler<UnarchiveHabitCommand, Result>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UnarchiveHabitCommandHandler(IHabitRepository habits, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(UnarchiveHabitCommand request, CancellationToken cancellationToken)
    {
        var user = HabitAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return user;
        }

        var owned = await HabitAccess.LoadOwnedAsync(_habits, user.Value, request.HabitId, cancellationToken);
        if (owned.IsFailure)
        {
            return owned;
        }

        owned.Value.Unarchive();
        _habits.Update(owned.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
