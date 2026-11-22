using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Mobility;

/// <summary>Creates a mobility routine (ordered drills with a target duration) for the current user.</summary>
/// <remarks>Kind defaults to a stretch. A follow-along needs a link and may have no drills.</remarks>
public sealed record CreateMobilityRoutineCommand(
    string Name,
    int TargetMinutes,
    IReadOnlyList<MobilityDrillDto> Drills,
    MobilityRoutineKind Kind = MobilityRoutineKind.Stretch,
    string? FollowAlongUrl = null) : ICommand<Result<MobilityRoutineDto>>;

public sealed class CreateMobilityRoutineCommandValidator : AbstractValidator<CreateMobilityRoutineCommand>
{
    public CreateMobilityRoutineCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TargetMinutes).GreaterThan(0);
        RuleFor(x => x.Drills).NotNull();
        RuleFor(x => x.Drills).NotEmpty().When(x => x.Kind != MobilityRoutineKind.FollowAlong);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.FollowAlongUrl).NotEmpty().MaximumLength(2000).When(x => x.Kind == MobilityRoutineKind.FollowAlong);
        RuleForEach(x => x.Drills).SetValidator(new MobilityDrillDtoValidator());
    }
}

public sealed class MobilityDrillDtoValidator : AbstractValidator<MobilityDrillDto>
{
    public MobilityDrillDtoValidator()
    {
        RuleFor(d => d.Name).NotEmpty().MaximumLength(200);
        RuleFor(d => d.Sets).GreaterThan(0);
        RuleFor(d => d.Dose).NotNull();
        RuleFor(d => d.Dose.Amount).GreaterThan(0).When(d => d.Dose is not null);
        RuleFor(d => d.Dose.Unit).IsInEnum().When(d => d.Dose is not null);
        RuleFor(d => d.ExtraSetSide).IsInEnum();
        RuleFor(d => d.Cues).MaximumLength(500);
        RuleFor(d => d.StandingAlternative).MaximumLength(500);
        RuleFor(d => d.Levels).Must(l => l is null || l.Count <= 10).WithMessage("A drill has at most 10 levels");
        RuleForEach(d => d.Levels).ChildRules(level =>
        {
            level.RuleFor(l => l.Name).NotEmpty().MaximumLength(100);
            level.RuleFor(l => l.Description).MaximumLength(300);
            level.RuleFor(l => l.Cues).MaximumLength(500);
            level.RuleFor(l => l.Sets).GreaterThan(0);
            level.RuleFor(l => l.Dose).NotNull();
            level.RuleFor(l => l.Dose.Amount).GreaterThan(0).When(l => l.Dose is not null);
            level.RuleFor(l => l.Range!.Min).GreaterThan(0).When(l => l.Range is not null);
            level.RuleFor(l => l.Range!.Max).GreaterThanOrEqualTo(l => l.Range!.Min).When(l => l.Range is not null);
            level.RuleFor(l => l.Range!.Step).GreaterThan(0).When(l => l.Range is not null);
        });
    }
}

public sealed class CreateMobilityRoutineCommandHandler : IRequestHandler<CreateMobilityRoutineCommand, Result<MobilityRoutineDto>>
{
    private readonly IMobilityRoutineRepository _routines;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public CreateMobilityRoutineCommandHandler(IMobilityRoutineRepository routines, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _routines = routines;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<MobilityRoutineDto>> Handle(CreateMobilityRoutineCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<MobilityRoutineDto>(user);
        }

        try
        {
            var routine = MobilityRoutine.Create(user.Value, request.Name, request.TargetMinutes,
                (request.Drills ?? []).Select(d => d.ToDomain()), request.Kind, request.FollowAlongUrl);
            await _routines.AddAsync(routine, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(routine.ToDto());
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<MobilityRoutineDto>(ex);
        }
    }
}
