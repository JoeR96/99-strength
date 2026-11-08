using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Mobility;

/// <summary>Replaces every drill in a mobility routine, in the given order.</summary>
public sealed record ReplaceDrillsCommand(Guid RoutineId, IReadOnlyList<MobilityDrillDto> Drills) : ICommand<Result<MobilityRoutineDto>>;

public sealed class ReplaceDrillsCommandValidator : AbstractValidator<ReplaceDrillsCommand>
{
    public ReplaceDrillsCommandValidator()
    {
        RuleFor(x => x.RoutineId).NotEmpty();
        RuleFor(x => x.Drills).NotEmpty();
        RuleForEach(x => x.Drills).SetValidator(new MobilityDrillDtoValidator());
    }
}

public sealed class ReplaceDrillsCommandHandler : IRequestHandler<ReplaceDrillsCommand, Result<MobilityRoutineDto>>
{
    private readonly IMobilityRoutineRepository _routines;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public ReplaceDrillsCommandHandler(IMobilityRoutineRepository routines, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _routines = routines;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<MobilityRoutineDto>> Handle(ReplaceDrillsCommand request, CancellationToken cancellationToken)
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
            routine.ReplaceDrills(request.Drills.Select(d => d.ToDomain()));
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
