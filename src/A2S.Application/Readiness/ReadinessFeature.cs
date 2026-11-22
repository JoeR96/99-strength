using System.Text.Json.Serialization;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Readiness;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Readiness;

/// <summary>
/// A day's readiness and what Today suggests because of it. <see cref="Colour"/> is null until the person taps one.
/// Suggestions only: nothing in a plan, workout or track changes.
/// </summary>
public sealed record ReadinessDto(
    DateOnly Date,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ReadinessColour>))] ReadinessColour? Colour,
    bool KeepFinisher,
    bool FinisherSkipped,
    bool SkipHardCardio,
    string? Headline,
    IReadOnlyList<string> Suggestions,
    DateTimeOffset? UpdatedAtUtc)
{
    public static ReadinessDto From(DateOnly date, ReadinessEntry? entry)
    {
        var advice = ReadinessAdvice.For(entry);
        return new ReadinessDto(date, entry?.Colour, entry?.KeepFinisher ?? false, advice.SkipFinisher, advice.SkipHardCardio,
            advice.Headline, advice.Suggestions, entry?.UpdatedAtUtc);
    }
}

/// <summary>The readiness for a local date (today when omitted).</summary>
public sealed record GetReadinessQuery(DateOnly? Date = null) : IQuery<Result<ReadinessDto>>;

public sealed class GetReadinessQueryHandler : IRequestHandler<GetReadinessQuery, Result<ReadinessDto>>
{
    private readonly IReadinessRepository _readiness;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public GetReadinessQueryHandler(IReadinessRepository readiness, ICurrentUserService currentUser, ILocalClock clock)
    {
        _readiness = readiness;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<ReadinessDto>> Handle(GetReadinessQuery request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<ReadinessDto>(user);
        }

        var date = request.Date ?? _clock.Today();
        var entry = await _readiness.GetForDateAsync(user.Value, date, cancellationToken);
        return Result.Success(ReadinessDto.From(date, entry));
    }
}

/// <summary>Sets a day's readiness colour (one tap on Today). Creates the entry the first time.</summary>
public sealed record SetReadinessCommand(
    DateOnly Date,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ReadinessColour>))] ReadinessColour Colour) : ICommand<Result<ReadinessDto>>;

public sealed class SetReadinessCommandValidator : AbstractValidator<SetReadinessCommand>
{
    public SetReadinessCommandValidator()
    {
        RuleFor(x => x.Colour).IsInEnum();
    }
}

public sealed class SetReadinessCommandHandler : IRequestHandler<SetReadinessCommand, Result<ReadinessDto>>
{
    private readonly IReadinessRepository _readiness;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public SetReadinessCommandHandler(IReadinessRepository readiness, IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILocalClock clock)
    {
        _readiness = readiness;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<ReadinessDto>> Handle(SetReadinessCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<ReadinessDto>(user);
        }

        try
        {
            var entry = await _readiness.GetForDateAsync(user.Value, request.Date, cancellationToken);
            if (entry is null)
            {
                entry = ReadinessEntry.Create(user.Value, request.Date, request.Colour, _clock.NowUtc());
                await _readiness.AddAsync(entry, cancellationToken);
            }
            else
            {
                entry.SetColour(request.Colour, _clock.NowUtc());
                _readiness.Update(entry);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(ReadinessDto.From(request.Date, entry));
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<ReadinessDto>(ex);
        }
    }
}

/// <summary>On a red day: keep the finisher (true, the one-tap undo) or skip it again (false).</summary>
public sealed record SetReadinessFinisherCommand(DateOnly Date, bool Keep) : ICommand<Result<ReadinessDto>>;

public sealed class SetReadinessFinisherCommandHandler : IRequestHandler<SetReadinessFinisherCommand, Result<ReadinessDto>>
{
    private readonly IReadinessRepository _readiness;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public SetReadinessFinisherCommandHandler(IReadinessRepository readiness, IUnitOfWork unitOfWork, ICurrentUserService currentUser, ILocalClock clock)
    {
        _readiness = readiness;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<ReadinessDto>> Handle(SetReadinessFinisherCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<ReadinessDto>(user);
        }

        var entry = await _readiness.GetForDateAsync(user.Value, request.Date, cancellationToken);
        if (entry is null)
        {
            return Result.Failure<ReadinessDto>("No readiness recorded for that day.", ErrorCode.NotFound);
        }

        try
        {
            entry.SetKeepFinisher(request.Keep, _clock.NowUtc());
            _readiness.Update(entry);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(ReadinessDto.From(request.Date, entry));
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<ReadinessDto>(ex);
        }
    }
}
