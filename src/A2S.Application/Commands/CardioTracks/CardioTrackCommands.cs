using A2S.Application.Cardio;
using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Hevy;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.CardioTracks;

/// <summary>User scoping and error mapping shared by the cardio track handlers.</summary>
internal static class TrackAccess
{
    public static async Task<Result<CardioTrack>> LoadOwnedAsync(ICardioTrackRepository tracks, ICurrentUserService currentUser, Guid id, CancellationToken ct)
    {
        var user = PlanAccess.RequireUser(currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<CardioTrack>(user);
        }

        var track = await tracks.GetByIdAsync(new CardioTrackId(id), ct);
        return track is null || track.UserId != user.Value
            ? Result.Failure<CardioTrack>("Cardio track not found.", ErrorCode.NotFound)
            : Result.Success(track);
    }

    /// <summary>Load → mutate → save, mapping domain rule failures to a Result.</summary>
    public static async Task<Result<CardioTrackDto>> MutateAsync(
        ICardioTrackRepository tracks,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILocalClock clock,
        Guid id,
        Action<CardioTrack> mutate,
        CancellationToken ct)
    {
        var load = await LoadOwnedAsync(tracks, currentUser, id, ct);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<CardioTrackDto>(load);
        }

        try
        {
            mutate(load.Value);
            tracks.Update(load.Value);
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(load.Value.ToDto(clock.Today()));
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<CardioTrackDto>(ex);
        }
    }
}

/// <summary>Starts a new track from a shipped template (an editable copy).</summary>
public sealed record CreateCardioTrackCommand(string TemplateKey, string? Name = null) : ICommand<Result<CardioTrackDto>>;

public sealed class CreateCardioTrackCommandValidator : AbstractValidator<CreateCardioTrackCommand>
{
    public CreateCardioTrackCommandValidator()
    {
        RuleFor(x => x.TemplateKey).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).MaximumLength(200);
    }
}

public sealed class CreateCardioTrackCommandHandler : IRequestHandler<CreateCardioTrackCommand, Result<CardioTrackDto>>
{
    private readonly ICardioTrackRepository _tracks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public CreateCardioTrackCommandHandler(ICardioTrackRepository tracks, IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILocalClock clock)
    {
        _tracks = tracks;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<CardioTrackDto>> Handle(CreateCardioTrackCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<CardioTrackDto>(user);
        }

        try
        {
            var today = _clock.Today();
            var track = CardioTrackTemplates.Create(request.TemplateKey, user.Value, today);
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                track.Rename(request.Name);
            }

            await _tracks.AddAsync(track, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(track.ToDto(today));
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<CardioTrackDto>(ex);
        }
    }
}

/// <summary>Edits a track's name, notes, stages (ranges, steps, sessions to advance) and benchmark.</summary>
public sealed record UpdateCardioTrackCommand(
    Guid Id,
    string Name,
    string? Notes,
    IReadOnlyList<TrackStageDto> Stages,
    TrackBenchmarkDto? Benchmark) : ICommand<Result<CardioTrackDto>>;

public sealed class UpdateCardioTrackCommandValidator : AbstractValidator<UpdateCardioTrackCommand>
{
    public UpdateCardioTrackCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Stages).NotEmpty();
    }
}

public sealed class UpdateCardioTrackCommandHandler : IRequestHandler<UpdateCardioTrackCommand, Result<CardioTrackDto>>
{
    private readonly ICardioTrackRepository _tracks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public UpdateCardioTrackCommandHandler(ICardioTrackRepository tracks, IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILocalClock clock)
    {
        _tracks = tracks;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    public Task<Result<CardioTrackDto>> Handle(UpdateCardioTrackCommand request, CancellationToken cancellationToken) =>
        TrackAccess.MutateAsync(_tracks, _unitOfWork, _currentUser, _clock, request.Id, track =>
        {
            var stages = request.Stages.Select(s => s.ToDomain()).ToList();
            track.Rename(request.Name);
            track.SetNotes(request.Notes);
            track.SetBenchmark(request.Benchmark is { } b ? new TrackBenchmark(b.Name, b.Unit, b.HigherIsBetter, b.EveryWeeks) : null);
            track.UpdateStages(stages, _clock.NowUtc());
        }, cancellationToken);
}

/// <summary>
/// Logs a session against a track and evaluates it. <see cref="ClientKey"/> makes offline retries
/// idempotent; without one every call is a new session.
/// </summary>
public sealed record LogCardioTrackSessionCommand(
    Guid Id,
    CardioLogDto Log,
    bool CompletedAsPrescribed = true,
    DateOnly? Date = null,
    string? ClientKey = null) : ICommand<Result<TrackEvaluationDto>>;

public sealed class LogCardioTrackSessionCommandValidator : AbstractValidator<LogCardioTrackSessionCommand>
{
    public LogCardioTrackSessionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Log).NotNull();
        RuleFor(x => x.ClientKey).MaximumLength(100);
        When(x => x.Log is not null, () =>
        {
            RuleFor(x => x.Log.DurationSeconds).GreaterThan(0);
            RuleFor(x => x.Log.Rpe).InclusiveBetween(1, 10).When(x => x.Log.Rpe.HasValue);
            RuleFor(x => x.Log.AvgHeartRate).InclusiveBetween(30, 250).When(x => x.Log.AvgHeartRate.HasValue);
            RuleFor(x => x.Log.MaxHeartRate).InclusiveBetween(30, 250).When(x => x.Log.MaxHeartRate.HasValue);
            RuleFor(x => x.Log.Effort).IsInEnum().When(x => x.Log.Effort.HasValue);
            RuleFor(x => x.Log.Pain).IsInEnum();
            RuleFor(x => x.Log.PainArea).MaximumLength(100);
        });
    }
}

public sealed class LogCardioTrackSessionCommandHandler : IRequestHandler<LogCardioTrackSessionCommand, Result<TrackEvaluationDto>>
{
    private readonly CardioTrackSessionRecorder _recorder;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    private readonly SessionHevyPoster? _hevyPoster;

    public LogCardioTrackSessionCommandHandler(
        CardioTrackSessionRecorder recorder,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILocalClock clock,
        SessionHevyPoster? hevyPoster = null)
    {
        _hevyPoster = hevyPoster;
        _recorder = recorder;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<TrackEvaluationDto>> Handle(LogCardioTrackSessionCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<TrackEvaluationDto>(user);
        }

        try
        {
            var key = string.IsNullOrWhiteSpace(request.ClientKey) ? $"manual:{Guid.NewGuid():N}" : $"client:{request.ClientKey.Trim()}";
            var date = request.Date ?? _clock.Today();
            var recorded = await _recorder.RecordAsync(user.Value, new CardioTrackId(request.Id), key, date, request.Log.ToDomain(),
                request.CompletedAsPrescribed, _clock.NowUtc(), cancellationToken);
            if (recorded is not { } r)
            {
                return Result.Failure<TrackEvaluationDto>("Cardio track not found.", ErrorCode.NotFound);
            }

            // Posted to Hevy from the server; the client key (a UUID from the phone) makes it idempotent.
            var post = _hevyPoster is not null && !r.Result.AlreadyRecorded
                ? await _hevyPoster.StageForTrackAsync(r.Track, ClientSessionId(request.ClientKey), date, request.Log.ToDomain(), _clock.NowUtc(), cancellationToken)
                : null;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (_hevyPoster is not null)
            {
                await _hevyPoster.TryPostAsync(post, cancellationToken);
            }

            return Result.Success(new TrackEvaluationDto(r.Result.Outcome, r.Result.Reason, r.Result.Qualified, r.Result.AlreadyRecorded,
                r.Track.ToDto(_clock.Today())));
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<TrackEvaluationDto>(ex);
        }
    }

    private static Guid ClientSessionId(string? clientKey) =>
        Guid.TryParse(clientKey, out var id) && id != Guid.Empty ? id : Guid.NewGuid();
}

/// <summary>Accepts or declines the track's pending suggestion (repeat week, drop back, next stage).</summary>
public sealed record DecideCardioTrackSuggestionCommand(Guid Id, bool Accept) : ICommand<Result<CardioTrackDto>>;

public sealed class DecideCardioTrackSuggestionCommandHandler : IRequestHandler<DecideCardioTrackSuggestionCommand, Result<CardioTrackDto>>
{
    private readonly ICardioTrackRepository _tracks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public DecideCardioTrackSuggestionCommandHandler(ICardioTrackRepository tracks, IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILocalClock clock)
    {
        _tracks = tracks;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    public Task<Result<CardioTrackDto>> Handle(DecideCardioTrackSuggestionCommand request, CancellationToken cancellationToken) =>
        TrackAccess.MutateAsync(_tracks, _unitOfWork, _currentUser, _clock, request.Id, track =>
        {
            if (request.Accept)
            {
                track.AcceptSuggestion(_clock.NowUtc(), _clock.Today());
            }
            else
            {
                track.DeclineSuggestion(_clock.NowUtc(), _clock.Today());
            }
        }, cancellationToken);
}

/// <summary>Restores the exact state before the track's last evaluation, decision or edit.</summary>
public sealed record UndoCardioTrackCommand(Guid Id) : ICommand<Result<CardioTrackDto>>;

public sealed class UndoCardioTrackCommandHandler : IRequestHandler<UndoCardioTrackCommand, Result<CardioTrackDto>>
{
    private readonly ICardioTrackRepository _tracks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public UndoCardioTrackCommandHandler(ICardioTrackRepository tracks, IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILocalClock clock)
    {
        _tracks = tracks;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    public Task<Result<CardioTrackDto>> Handle(UndoCardioTrackCommand request, CancellationToken cancellationToken) =>
        TrackAccess.MutateAsync(_tracks, _unitOfWork, _currentUser, _clock, request.Id, track => track.UndoLastEvaluation(), cancellationToken);
}

/// <summary>Pauses (true) or resumes (false) a track. Paused tracks record sessions without progressing.</summary>
public sealed record SetCardioTrackPausedCommand(Guid Id, bool Paused) : ICommand<Result<CardioTrackDto>>;

public sealed class SetCardioTrackPausedCommandHandler : IRequestHandler<SetCardioTrackPausedCommand, Result<CardioTrackDto>>
{
    private readonly ICardioTrackRepository _tracks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public SetCardioTrackPausedCommandHandler(ICardioTrackRepository tracks, IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILocalClock clock)
    {
        _tracks = tracks;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    public Task<Result<CardioTrackDto>> Handle(SetCardioTrackPausedCommand request, CancellationToken cancellationToken) =>
        TrackAccess.MutateAsync(_tracks, _unitOfWork, _currentUser, _clock, request.Id, track =>
        {
            if (request.Paused)
            {
                track.Pause();
            }
            else
            {
                track.Resume();
            }
        }, cancellationToken);
}

/// <summary>Records a benchmark result (PR history). Never changes progression.</summary>
public sealed record RecordCardioBenchmarkCommand(Guid Id, decimal Value, DateOnly? Date = null, string? Notes = null) : ICommand<Result<BenchmarkResultDto>>;

public sealed class RecordCardioBenchmarkCommandValidator : AbstractValidator<RecordCardioBenchmarkCommand>
{
    public RecordCardioBenchmarkCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class RecordCardioBenchmarkCommandHandler : IRequestHandler<RecordCardioBenchmarkCommand, Result<BenchmarkResultDto>>
{
    private readonly ICardioTrackRepository _tracks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public RecordCardioBenchmarkCommandHandler(ICardioTrackRepository tracks, IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILocalClock clock)
    {
        _tracks = tracks;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<BenchmarkResultDto>> Handle(RecordCardioBenchmarkCommand request, CancellationToken cancellationToken)
    {
        var load = await TrackAccess.LoadOwnedAsync(_tracks, _currentUser, request.Id, cancellationToken);
        if (load.IsFailure)
        {
            return PlanAccess.Fail<BenchmarkResultDto>(load);
        }

        try
        {
            var result = load.Value.RecordBenchmark(request.Date ?? _clock.Today(), request.Value, request.Notes);
            _tracks.Update(load.Value);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(new BenchmarkResultDto(result.Date, result.Value, result.Notes, result.IsPersonalBest));
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<BenchmarkResultDto>(ex);
        }
    }
}

/// <summary>Sets the user's max heart rate and (optionally) custom zone boundaries.</summary>
public sealed record SetHeartRateZonesCommand(int? MaxHeartRate, IReadOnlyList<int>? LowerPercents) : ICommand<Result<HeartRateZonesDto>>;

public sealed class SetHeartRateZonesCommandHandler : IRequestHandler<SetHeartRateZonesCommand, Result<HeartRateZonesDto>>
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public SetHeartRateZonesCommandHandler(IUserRepository users, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<HeartRateZonesDto>> Handle(SetHeartRateZonesCommand request, CancellationToken cancellationToken)
    {
        var userId = PlanAccess.RequireUser(_currentUser);
        if (userId.IsFailure)
        {
            return PlanAccess.Fail<HeartRateZonesDto>(userId);
        }

        var user = await _users.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return Result.Failure<HeartRateZonesDto>("User not found.", ErrorCode.NotFound);
        }

        try
        {
            user.SetHeartRate(request.MaxHeartRate, request.LowerPercents);
            _users.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(CardioTrackMappings.ToZonesDto(user.MaxHeartRate, user.HeartRateZonePercents));
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<HeartRateZonesDto>(ex);
        }
    }
}
