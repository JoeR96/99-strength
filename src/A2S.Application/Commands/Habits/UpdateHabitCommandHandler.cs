using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Habits;

public sealed class UpdateHabitCommandHandler : IRequestHandler<UpdateHabitCommand, Result<HabitDto>>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UpdateHabitCommandHandler(IHabitRepository habits, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<HabitDto>> Handle(UpdateHabitCommand request, CancellationToken cancellationToken)
    {
        var user = HabitAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return HabitAccess.Fail<HabitDto>(user);
        }

        var owned = await HabitAccess.LoadOwnedAsync(_habits, user.Value, request.HabitId, cancellationToken);
        if (owned.IsFailure)
        {
            return HabitAccess.Fail<HabitDto>(owned);
        }

        try
        {
            var habit = owned.Value;
            habit.Rename(request.Name);
            habit.SetTargets(request.Target, request.Minimum);
            habit.SetUnit(request.Unit);
            habit.SetOrder(request.Order);
            habit.SetIcon(request.Icon);

            _habits.Update(habit);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(HabitDto.From(habit));
        }
        catch (BusinessRuleViolationException ex)
        {
            return Result.Failure<HabitDto>(ex.Message, ErrorCode.DomainRuleViolation);
        }
    }
}
