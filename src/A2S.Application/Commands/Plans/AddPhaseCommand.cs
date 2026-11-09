using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Plans;

/// <summary>Adds a dated phase (cut, maintenance, pause, deload) to the plan. Phases may not overlap.</summary>
public sealed record AddPhaseCommand(Guid PlanId, PhaseType Type, DateOnly From, DateOnly To, string? Note) : ICommand<Result<TrainingPlanDto>>;

public sealed class AddPhaseCommandValidator : AbstractValidator<AddPhaseCommand>
{
    public AddPhaseCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("Phase end must be on or after its start.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class AddPhaseCommandHandler : IRequestHandler<AddPhaseCommand, Result<TrainingPlanDto>>
{
    private readonly ITrainingPlanRepository _plans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public AddPhaseCommandHandler(ITrainingPlanRepository plans, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _plans = plans;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<TrainingPlanDto>> Handle(AddPhaseCommand request, CancellationToken cancellationToken)
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
            load.Value.AddPhase(new PlanPhase(request.Type, request.From, request.To, request.Note));
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
