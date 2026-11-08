using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Habits;

public sealed class UpsertHabitLogCommandHandler : IRequestHandler<UpsertHabitLogCommand, Result<HabitLogDto>>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UpsertHabitLogCommandHandler(IHabitRepository habits, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<HabitLogDto>> Handle(UpsertHabitLogCommand request, CancellationToken cancellationToken)
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
                log = HabitLog.Create(habit.Id, user.Value, request.Date, request.Value, request.ClientTimestamp);
                await _habits.AddLogAsync(log, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else if (log.Apply(request.Value, request.ClientTimestamp))
            {
                _habits.UpdateLog(log);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Success(HabitLogDto.From(log));
        }
        catch (BusinessRuleViolationException ex)
        {
            return Result.Failure<HabitLogDto>(ex.Message, ErrorCode.DomainRuleViolation);
        }
    }
}
