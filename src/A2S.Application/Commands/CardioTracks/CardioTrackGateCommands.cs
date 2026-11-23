using A2S.Domain.Aggregates.Sync;
using A2S.Application.Sync;
using A2S.Application.Cardio;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.CardioTracks;

/// <summary>
/// Sets a track's start gate. <see cref="UseDefault"/> applies the template's gate (Walk → run: the running gate);
/// otherwise <see cref="Conditions"/> is used, and an empty list removes the gate.
/// </summary>
[ChangesSyncArea(SyncArea.Cardio)]
public sealed record SetCardioTrackGateCommand(Guid Id, IReadOnlyList<GateConditionDto>? Conditions, bool UseDefault = false)
    : ICommand<Result<CardioTrackDto>>;

public sealed class SetCardioTrackGateCommandValidator : AbstractValidator<SetCardioTrackGateCommand>
{
    public SetCardioTrackGateCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Conditions).Must(c => c is null || c.Count <= 10).WithMessage("A gate holds at most 10 conditions.");
    }
}

public sealed class SetCardioTrackGateCommandHandler : IRequestHandler<SetCardioTrackGateCommand, Result<CardioTrackDto>>
{
    private readonly ICardioTrackRepository _tracks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public SetCardioTrackGateCommandHandler(ICardioTrackRepository tracks, IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILocalClock clock)
    {
        _tracks = tracks;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    public Task<Result<CardioTrackDto>> Handle(SetCardioTrackGateCommand request, CancellationToken cancellationToken) =>
        TrackAccess.MutateAsync(_tracks, _unitOfWork, _currentUser, _clock, request.Id, track =>
        {
            var conditions = request.UseDefault
                ? CardioTrackTemplates.DefaultGate(track.TemplateKey ?? string.Empty)
                : (request.Conditions ?? []).Select(c => c.ToDomain()).ToList();
            track.SetStartGate(conditions, _clock.NowUtc());
        }, cancellationToken);
}

/// <summary>
/// Accepts the "Start" suggestion of a gated track. The person decides: the suggestion appears once every
/// condition is met, and starting early is their call.
/// </summary>
[ChangesSyncArea(SyncArea.Cardio)]
public sealed record StartGatedCardioTrackCommand(Guid Id) : ICommand<Result<CardioTrackDto>>;

public sealed class StartGatedCardioTrackCommandHandler : IRequestHandler<StartGatedCardioTrackCommand, Result<CardioTrackDto>>
{
    private readonly ICardioTrackRepository _tracks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public StartGatedCardioTrackCommandHandler(ICardioTrackRepository tracks, IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILocalClock clock)
    {
        _tracks = tracks;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    public Task<Result<CardioTrackDto>> Handle(StartGatedCardioTrackCommand request, CancellationToken cancellationToken) =>
        TrackAccess.MutateAsync(_tracks, _unitOfWork, _currentUser, _clock, request.Id,
            track => track.StartFromGate(_clock.Today(), _clock.NowUtc()), cancellationToken);
}
