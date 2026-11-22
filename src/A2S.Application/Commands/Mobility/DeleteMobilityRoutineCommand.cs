using A2S.Application.Common;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Mobility;

/// <summary>
/// Deletes a mobility routine. A routine still scheduled in one of the user's plans can't be deleted
/// (take it out of the week first), so no slot is left pointing at nothing. Past completions keep
/// their routine id as history.
/// </summary>
public sealed record DeleteMobilityRoutineCommand(Guid RoutineId) : ICommand<Result>;

public sealed class DeleteMobilityRoutineCommandValidator : AbstractValidator<DeleteMobilityRoutineCommand>
{
    public DeleteMobilityRoutineCommandValidator()
    {
        RuleFor(x => x.RoutineId).NotEmpty();
    }
}

public sealed class DeleteMobilityRoutineCommandHandler : IRequestHandler<DeleteMobilityRoutineCommand, Result>
{
    private readonly IMobilityRoutineRepository _routines;
    private readonly ITrainingPlanRepository _plans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public DeleteMobilityRoutineCommandHandler(
        IMobilityRoutineRepository routines,
        ITrainingPlanRepository plans,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _routines = routines;
        _plans = plans;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteMobilityRoutineCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return Result.Failure(user.Error, user.ErrorCode);
        }

        var routine = await _routines.GetByIdAsync(new MobilityRoutineId(request.RoutineId), cancellationToken);
        if (routine is null || routine.UserId != user.Value)
        {
            return Result.Failure("Mobility routine not found.", ErrorCode.NotFound);
        }

        var plans = await _plans.ListForUserAsync(user.Value, cancellationToken);
        var scheduledIn = plans.FirstOrDefault(p => p.SchedulesMobilityRoutine(routine.Id));
        if (scheduledIn is not null)
        {
            return Result.Failure(
                $"'{routine.Name}' is in the weekly schedule of '{scheduledIn.Name}'. Take it out of the week first.",
                ErrorCode.Conflict);
        }

        _routines.Remove(routine);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
