using A2S.Application.Common;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Hevy;

/// <summary>Dismisses an inbox workout: it stays stored (so a replayed event does not bring it back) but leaves the inbox.</summary>
public sealed record IgnoreHevyWorkoutCommand(string HevyWorkoutId) : ICommand<Result>;

public sealed class IgnoreHevyWorkoutCommandValidator : AbstractValidator<IgnoreHevyWorkoutCommand>
{
    public IgnoreHevyWorkoutCommandValidator()
    {
        RuleFor(x => x.HevyWorkoutId).NotEmpty().MaximumLength(100);
    }
}

public sealed class IgnoreHevyWorkoutCommandHandler : IRequestHandler<IgnoreHevyWorkoutCommand, Result>
{
    private readonly IHevyWorkoutSnapshotRepository _snapshots;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalClock _clock;

    public IgnoreHevyWorkoutCommandHandler(IHevyWorkoutSnapshotRepository snapshots, ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILocalClock clock)
    {
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result> Handle(IgnoreHevyWorkoutCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return user;
        }

        var snapshot = await _snapshots.GetByIdAsync(new HevyWorkoutId(request.HevyWorkoutId), cancellationToken);
        if (snapshot is null || snapshot.UserId != user.Value)
        {
            return Result.Failure("Hevy workout not found.", ErrorCode.NotFound);
        }

        try
        {
            snapshot.Ignore(_clock.NowUtc());
        }
        catch (DomainException ex)
        {
            return Result.Failure(ex.Message, ErrorCode.DomainRuleViolation);
        }

        _snapshots.Update(snapshot);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
