using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.Cardio;

/// <summary>
/// Creates a cardio protocol for the current user: Norwegian 4×4 (no parameters), a Couch to 5K
/// week (<paramref name="WeekIndex"/> 1–9, optional repeat) or LISS (<paramref name="Minutes"/> 30–40, optional note).
/// </summary>
public sealed record CreateCardioProtocolCommand(
    CardioProtocolKind Kind,
    int? WeekIndex = null,
    bool RepeatWeek = false,
    int? Minutes = null,
    string? Note = null) : ICommand<Result<CardioProtocolDto>>;

public sealed class CreateCardioProtocolCommandValidator : AbstractValidator<CreateCardioProtocolCommand>
{
    public CreateCardioProtocolCommandValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        When(x => x.Kind == CardioProtocolKind.CouchToFiveK, () =>
        {
            RuleFor(x => x.WeekIndex).NotNull()
                .InclusiveBetween(CardioProtocol.CouchToFiveKFirstWeek, CardioProtocol.CouchToFiveKLastWeek)
                .WithMessage($"Couch to 5K week must be between {CardioProtocol.CouchToFiveKFirstWeek} and {CardioProtocol.CouchToFiveKLastWeek}.");
        });
        When(x => x.Kind == CardioProtocolKind.Liss, () =>
        {
            RuleFor(x => x.Minutes).NotNull()
                .InclusiveBetween(CardioProtocol.LissMinimumMinutes, CardioProtocol.LissMaximumMinutes)
                .WithMessage($"LISS duration must be between {CardioProtocol.LissMinimumMinutes} and {CardioProtocol.LissMaximumMinutes} minutes.");
        });
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class CreateCardioProtocolCommandHandler : IRequestHandler<CreateCardioProtocolCommand, Result<CardioProtocolDto>>
{
    private readonly ICardioProtocolRepository _protocols;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public CreateCardioProtocolCommandHandler(ICardioProtocolRepository protocols, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _protocols = protocols;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<CardioProtocolDto>> Handle(CreateCardioProtocolCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<CardioProtocolDto>(user);
        }

        try
        {
            var protocol = request.Kind switch
            {
                CardioProtocolKind.NorwegianFourByFour => CardioProtocol.NorwegianFourByFour(user.Value),
                CardioProtocolKind.CouchToFiveK => CardioProtocol.CouchToFiveK(user.Value, request.WeekIndex ?? 0, request.RepeatWeek),
                CardioProtocolKind.Liss => CardioProtocol.Liss(user.Value, request.Minutes ?? 0, request.Note),
                _ => throw new BusinessRuleViolationException($"Unknown cardio protocol kind '{request.Kind}'")
            };

            await _protocols.AddAsync(protocol, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(protocol.ToDto());
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<CardioProtocolDto>(ex);
        }
    }
}
