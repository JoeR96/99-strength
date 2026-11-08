using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Cardio;

/// <summary>Moves a Couch to 5K protocol to its next week (honouring a pending repeat; capped at week 9).</summary>
public sealed record AdvanceCouchToFiveKCommand(Guid ProtocolId) : ICommand<Result<CardioProtocolDto>>;

public sealed class AdvanceCouchToFiveKCommandHandler : IRequestHandler<AdvanceCouchToFiveKCommand, Result<CardioProtocolDto>>
{
    private readonly ICardioProtocolRepository _protocols;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public AdvanceCouchToFiveKCommandHandler(ICardioProtocolRepository protocols, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _protocols = protocols;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<CardioProtocolDto>> Handle(AdvanceCouchToFiveKCommand request, CancellationToken cancellationToken)
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
            protocol.NextWeek();
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
