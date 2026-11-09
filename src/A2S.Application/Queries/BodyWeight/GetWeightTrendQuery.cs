using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Interfaces;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using FluentValidation;
using MediatR;

namespace A2S.Application.Queries.BodyWeight;

/// <summary>
/// Weigh-ins between two dates smoothed into a trend, plus today's pace against the active plan's checkpoints
/// when there is one. The pace uses the latest trend point in the range.
/// </summary>
public sealed record GetWeightTrendQuery(DateOnly From, DateOnly To) : IQuery<Result<WeightTrendDto>>;

public sealed class GetWeightTrendQueryValidator : AbstractValidator<GetWeightTrendQuery>
{
    public GetWeightTrendQueryValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("'to' must not be before 'from'.");
    }
}

public sealed class GetWeightTrendQueryHandler : IRequestHandler<GetWeightTrendQuery, Result<WeightTrendDto>>
{
    private readonly IBodyWeightRepository _entries;
    private readonly IPlanCheckpointSource _checkpoints;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;
    private readonly TrendService _trend;

    public GetWeightTrendQueryHandler(
        IBodyWeightRepository entries,
        IPlanCheckpointSource checkpoints,
        ICurrentUserService currentUser,
        ILocalClock clock,
        TrendService trend)
    {
        _entries = entries ?? throw new ArgumentNullException(nameof(entries));
        _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _trend = trend ?? throw new ArgumentNullException(nameof(trend));
    }

    public async Task<Result<WeightTrendDto>> Handle(GetWeightTrendQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        if (userId is null)
        {
            return Result.Failure<WeightTrendDto>("User must be authenticated.", ErrorCode.Unauthenticated);
        }

        // The window starts a week early so the first visible points already have a 7-day mean and a warmed EWMA.
        var entries = await _entries.ListAsync(userId.Value, request.From.AddDays(-6), request.To, cancellationToken);
        var series = _trend.Series(entries);
        var visible = series.Where(p => p.Date >= request.From).Select(TrendPointDto.From).ToList();

        PaceDto? pace = null;
        var latest = series.Count == 0 ? null : series[^1];
        if (latest is not null)
        {
            var plan = await _checkpoints.ForUserAsync(userId.Value, cancellationToken);
            if (plan is not null)
            {
                var result = _trend.PaceAgainst(plan.Checkpoints, plan.StartDate, plan.StartKg, _clock.Today(), latest.Ewma);
                pace = PaceDto.From(result);
            }
        }

        return Result.Success(new WeightTrendDto(visible, pace));
    }
}
