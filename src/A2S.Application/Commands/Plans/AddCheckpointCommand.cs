using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Plans;

/// <summary>Adds a trend-weight checkpoint. Dates are unique within a plan.</summary>
public sealed record AddCheckpointCommand(Guid PlanId, DateOnly Date, decimal TargetKg, string? Note) : ICommand<Result<TrainingPlanDto>>;

public sealed class AddCheckpointCommandValidator : AbstractValidator<AddCheckpointCommand>
{
    public AddCheckpointCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.TargetKg).InclusiveBetween(Checkpoint.MinimumKg, Checkpoint.MaximumKg);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class AddCheckpointCommandHandler : IRequestHandler<AddCheckpointCommand, Result<TrainingPlanDto>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public AddCheckpointCommandHandler(ITrainingPlanRepository plans, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _plans = plans;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<TrainingPlanDto>> Handle(AddCheckpointCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<TrainingPlanDto>(user);
        }

        var load = await PlanAccess.LoadOwnedAsync(_plans, user.Value, request.PlanId, cancellationToken);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<TrainingPlanDto>(load);
        }

        try
        {
            load.Value.AddCheckpoint(new Checkpoint(request.Date, request.TargetKg, request.Note));
            _plans.Update(load.Value);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(load.Value.ToDto());
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<TrainingPlanDto>(ex);
        }
    }
}
