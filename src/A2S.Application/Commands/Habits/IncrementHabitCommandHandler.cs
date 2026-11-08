using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Habits;

public sealed class IncrementHabitCommandHandler : IRequestHandler<IncrementHabitCommand, Result<HabitLogDto>>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public IncrementHabitCommandHandler(IHabitRepository habits, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<HabitLogDto>> Handle(IncrementHabitCommand request, CancellationToken cancellationToken)
    {
        var user = HabitAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return HabitAccess.Fail<HabitLogDto>(user);
        }

        var owned = await HabitAccess.LoadOwnedAsync(_habits, user.Value, request.HabitId, cancellationToken);
        if (owned.IsFailure)
        {
            return HabitAccess.Fail<HabitLogDto>(owned);
        }

        try
        {
            var habit = owned.Value;
            var log = await _habits.GetLogAsync(habit.Id, request.Date, cancellationToken);

            if (log is null)
            {
                var initial = request.Delta > 0 ? 1 : 0;
                log = HabitLog.Create(habit.Id, user.Value, request.Date, initial, request.ClientTimestamp);
                await _habits.AddLogAsync(log, cancellationToken);
            }
            else
            {
                // Taps are applied in arrival order, but the stored timestamp never moves backwards, so a
                // late-arriving tap cannot reopen the door to an even older full-value write.
                var at = request.ClientTimestamp > log.UpdatedAtUtc ? request.ClientTimestamp : log.UpdatedAtUtc;

                if (habit.Type == HabitType.Checkbox)
                {
                    log.SetValue(request.Delta > 0 ? 1 : 0, at);
                }
                else if (request.Delta > 0)
                {
                    log.Increment(request.Delta, at);
                }
                else
                {
                    log.Decrement(-request.Delta, at);
                }

                _habits.UpdateLog(log);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(HabitLogDto.From(log));
        }
        catch (BusinessRuleViolationException ex)
        {
            return Result.Failure<HabitLogDto>(ex.Message, ErrorCode.DomainRuleViolation);
        }
    }
}
