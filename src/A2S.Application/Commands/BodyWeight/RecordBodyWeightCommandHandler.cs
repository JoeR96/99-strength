using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.BodyWeight;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.BodyWeight;

public sealed class RecordBodyWeightCommandHandler : IRequestHandler<RecordBodyWeightCommand, Result<BodyWeightEntryDto>>
{
    private readonly IBodyWeightRepository _entries;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public RecordBodyWeightCommandHandler(
        IBodyWeightRepository entries,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILocalClock clock)
    {
        _entries = entries ?? throw new ArgumentNullException(nameof(entries));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<BodyWeightEntryDto>> Handle(RecordBodyWeightCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        if (userId is null)
        {
            return Result.Failure<BodyWeightEntryDto>("User must be authenticated.", ErrorCode.Unauthenticated);
        }

        try
        {
            var now = _clock.NowUtc();
            var entry = await _entries.GetForDateAsync(userId.Value, request.Date, cancellationToken);

            if (entry is null)
            {
                entry = BodyWeightEntry.Create(userId.Value, request.Date, request.Kg, now);
                await _entries.AddAsync(entry, cancellationToken);
            }
            else
            {
                entry.Update(request.Kg, now);
                _entries.Update(entry);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(BodyWeightEntryDto.From(entry));
        }
        catch (BusinessRuleViolationException ex)
        {
            return Result.Failure<BodyWeightEntryDto>(ex.Message, ErrorCode.DomainRuleViolation);
        }
    }
}
