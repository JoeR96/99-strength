using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Mobility;

/// <summary>How one drill felt in a finished session.</summary>
public sealed record DrillFeedbackDto(string DrillName, DrillOutcome Outcome);

/// <summary>
/// Records a finished session's per-drill feedback (clean / strained / stopped / skipped) and runs the light
/// progression rule. Idempotent on <see cref="SessionKey"/> (the client's session id), so the offline queue can replay it.
/// </summary>
public sealed record RecordDrillFeedbackCommand(Guid RoutineId, string SessionKey, IReadOnlyList<DrillFeedbackDto> Feedback)
    : ICommand<Result<DrillFeedbackResultDto>>;

/// <summary>Accepts or declines a drill's pending "move up a level" suggestion.</summary>
public sealed record RespondToDrillSuggestionCommand(Guid RoutineId, string DrillName, bool Accept) : ICommand<Result<MobilityRoutineDto>>;

/// <summary>Reverts the routine's most recent progression change.</summary>
public sealed record UndoDrillProgressionCommand(Guid RoutineId) : ICommand<Result<MobilityRoutineDto>>;

/// <summary>Phase bump: adds seconds to every timed hold in the routine (e.g. phase 2: +15 s).</summary>
public sealed record ApplyHoldIncreaseCommand(Guid RoutineId, int Seconds) : ICommand<Result<MobilityRoutineDto>>;

public sealed class RecordDrillFeedbackCommandValidator : AbstractValidator<RecordDrillFeedbackCommand>
{
    public RecordDrillFeedbackCommandValidator()
    {
        RuleFor(x => x.RoutineId).NotEmpty();
        RuleFor(x => x.SessionKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Feedback).NotNull();
        RuleForEach(x => x.Feedback).ChildRules(f =>
        {
            f.RuleFor(i => i.DrillName).NotEmpty().MaximumLength(200);
            f.RuleFor(i => i.Outcome).IsInEnum();
        });
    }
}

public sealed class RespondToDrillSuggestionCommandValidator : AbstractValidator<RespondToDrillSuggestionCommand>
{
    public RespondToDrillSuggestionCommandValidator()
    {
        RuleFor(x => x.RoutineId).NotEmpty();
        RuleFor(x => x.DrillName).NotEmpty().MaximumLength(200);
    }
}

public sealed class ApplyHoldIncreaseCommandValidator : AbstractValidator<ApplyHoldIncreaseCommand>
{
    public ApplyHoldIncreaseCommandValidator()
    {
        RuleFor(x => x.RoutineId).NotEmpty();
        RuleFor(x => x.Seconds).InclusiveBetween(1, 120);
    }
}

/// <summary>Shared load-owned → mutate → save for the routine progression commands.</summary>
internal static class RoutineMutation
{
    public static async Task<Result<T>> RunAsync<T>(
        IMobilityRoutineRepository routines,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        Guid routineId,
        Func<MobilityRoutine, Result<T>> mutate,
        CancellationToken ct)
    {
        var user = PlanAccess.RequireUser(currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<T>(user);
        }

        var routine = await routines.GetByIdAsync(new MobilityRoutineId(routineId), ct);
        if (routine is null || routine.UserId != user.Value)
        {
            return Result.Failure<T>("Mobility routine not found.", ErrorCode.NotFound);
        }

        try
        {
            var result = mutate(routine);
            if (result.IsFailure)
            {
                return result;
            }

            routines.Update(routine);
            await unitOfWork.SaveChangesAsync(ct);
            return result;
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<T>(ex);
        }
    }
}

public sealed class RecordDrillFeedbackCommandHandler(
    IMobilityRoutineRepository routines, IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILocalClock clock)
    : IRequestHandler<RecordDrillFeedbackCommand, Result<DrillFeedbackResultDto>>
{
    public Task<Result<DrillFeedbackResultDto>> Handle(RecordDrillFeedbackCommand request, CancellationToken cancellationToken) =>
        RoutineMutation.RunAsync(routines, unitOfWork, currentUser, request.RoutineId, routine =>
        {
            var changes = routine.RecordFeedback(request.SessionKey,
                request.Feedback.Select(f => new DrillFeedback(f.DrillName, f.Outcome)), clock.NowUtc());
            return Result.Success(new DrillFeedbackResultDto(routine.ToDto(), changes.Select(c => c.ToDto()).ToList()));
        }, cancellationToken);
}

public sealed class RespondToDrillSuggestionCommandHandler(
    IMobilityRoutineRepository routines, IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILocalClock clock)
    : IRequestHandler<RespondToDrillSuggestionCommand, Result<MobilityRoutineDto>>
{
    public Task<Result<MobilityRoutineDto>> Handle(RespondToDrillSuggestionCommand request, CancellationToken cancellationToken) =>
        RoutineMutation.RunAsync(routines, unitOfWork, currentUser, request.RoutineId, routine =>
        {
            routine.RespondToSuggestion(request.DrillName, request.Accept, clock.NowUtc());
            return Result.Success(routine.ToDto());
        }, cancellationToken);
}

public sealed class UndoDrillProgressionCommandHandler(
    IMobilityRoutineRepository routines, IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILocalClock clock)
    : IRequestHandler<UndoDrillProgressionCommand, Result<MobilityRoutineDto>>
{
    public Task<Result<MobilityRoutineDto>> Handle(UndoDrillProgressionCommand request, CancellationToken cancellationToken) =>
        RoutineMutation.RunAsync(routines, unitOfWork, currentUser, request.RoutineId, routine =>
            routine.UndoLastChange(clock.NowUtc())
                ? Result.Success(routine.ToDto())
                : Result.Failure<MobilityRoutineDto>("There is no progression change to undo.", ErrorCode.Conflict),
            cancellationToken);
}

public sealed class ApplyHoldIncreaseCommandHandler(
    IMobilityRoutineRepository routines, IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILocalClock clock)
    : IRequestHandler<ApplyHoldIncreaseCommand, Result<MobilityRoutineDto>>
{
    public Task<Result<MobilityRoutineDto>> Handle(ApplyHoldIncreaseCommand request, CancellationToken cancellationToken) =>
        RoutineMutation.RunAsync(routines, unitOfWork, currentUser, request.RoutineId, routine =>
        {
            routine.ApplyHoldIncrease(request.Seconds, clock.NowUtc());
            return Result.Success(routine.ToDto());
        }, cancellationToken);
}
