using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Cardio;

/// <summary>Marks (or unmarks) the current Couch to 5K week to be repeated on the next advance.</summary>
public sealed record SetRepeatWeekCommand(Guid ProtocolId, bool Repeat) : ICommand<Result<CardioProtocolDto>>;

public sealed class SetRepeatWeekCommandHandler : IRequestHandler<SetRepeatWeekCommand, Result<CardioProtocolDto>>
{
    private readonly ICardioProtocolRepository _protocols;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public SetRepeatWeekCommandHandler(ICardioProtocolRepository protocols, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _protocols = protocols;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<CardioProtocolDto>> Handle(SetRepeatWeekCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<CardioProtocolDto>(user);
        }

        var protocol = await _protocols.GetByIdAsync(new CardioProtocolId(request.ProtocolId), cancellationToken);
        if (protocol is null || protocol.UserId != user.Value)
        {
            return Result.Failure<CardioProtocolDto>("Cardio protocol not found.", ErrorCode.NotFound);
        }

        try
        {
            if (request.Repeat)
            {
                protocol.WithRepeatWeek();
            }
            else
            {
                protocol.ClearRepeatWeek();
            }

            _protocols.Update(protocol);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(protocol.ToDto());
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<CardioProtocolDto>(ex);
        }
    }
}
