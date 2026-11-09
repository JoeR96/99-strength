using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Habits;

public sealed class CreateHabitCommandHandler : IRequestHandler<CreateHabitCommand, Result<HabitDto>>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public CreateHabitCommandHandler(IHabitRepository habits, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<HabitDto>> Handle(CreateHabitCommand request, CancellationToken cancellationToken)
    {
        var user = HabitAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return HabitAccess.Fail<HabitDto>(user);
        }

        try
        {
            var habit = HabitDefinition.Create(
                user.Value, request.Name, request.Type, request.Target, request.Minimum, request.Unit, request.Order, request.Icon);

            await _habits.AddAsync(habit, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(HabitDto.From(habit));
        }
        catch (BusinessRuleViolationException ex)
        {
            return Result.Failure<HabitDto>(ex.Message, ErrorCode.DomainRuleViolation);
        }
    }
}
