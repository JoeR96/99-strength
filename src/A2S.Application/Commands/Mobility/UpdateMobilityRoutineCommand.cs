using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Mobility;

/// <summary>Renames a mobility routine and changes its target minutes. Drills are left as they are.</summary>
public sealed record UpdateMobilityRoutineCommand(Guid RoutineId, string Name, int TargetMinutes) : ICommand<Result<MobilityRoutineDto>>;

public sealed class UpdateMobilityRoutineCommandValidator : AbstractValidator<UpdateMobilityRoutineCommand>
{
    public UpdateMobilityRoutineCommandValidator()
    {
        RuleFor(x => x.RoutineId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().Must(n => !string.IsNullOrWhiteSpace(n)).MaximumLength(200);
        RuleFor(x => x.TargetMinutes).GreaterThan(0);
    }
}

public sealed class UpdateMobilityRoutineCommandHandler : IRequestHandler<UpdateMobilityRoutineCommand, Result<MobilityRoutineDto>>
{
    private readonly IMobilityRoutineRepository _routines;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UpdateMobilityRoutineCommandHandler(IMobilityRoutineRepository routines, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _routines = routines;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<MobilityRoutineDto>> Handle(UpdateMobilityRoutineCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<MobilityRoutineDto>(user);
        }

        var routine = await _routines.GetByIdAsync(new MobilityRoutineId(request.RoutineId), cancellationToken);
        if (routine is null || routine.UserId != user.Value)
        {
            return Result.Failure<MobilityRoutineDto>("Mobility routine not found.", ErrorCode.NotFound);
        }

        try
        {
            routine.UpdateDetails(request.Name, request.TargetMinutes);
            _routines.Update(routine);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(routine.ToDto());
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<MobilityRoutineDto>(ex);
        }
    }
}
