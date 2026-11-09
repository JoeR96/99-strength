using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Aggregates.ApiTokens;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentValidation;
using MediatR;

namespace A2S.Application.Commands.ApiTokens;

/// <summary>
/// Mints a personal API token for the caller. The response carries the plaintext once; only its hash
/// is stored. A user can hold at most <see cref="ApiToken.MaxActivePerUser"/> unrevoked tokens.
/// </summary>
public sealed record CreateApiTokenCommand(string Name) : ICommand<Result<CreatedApiTokenDto>>;

public sealed class CreateApiTokenCommandValidator : AbstractValidator<CreateApiTokenCommand>
{
    public CreateApiTokenCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A token needs a name.")
            .Must(name => name is null || name.Trim().Length <= ApiToken.MaxNameLength)
            .WithMessage($"A token name cannot exceed {ApiToken.MaxNameLength} characters.");
    }
}

public sealed class CreateApiTokenCommandHandler : IRequestHandler<CreateApiTokenCommand, Result<CreatedApiTokenDto>>
{
    private readonly IApiTokenRepository _tokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public CreateApiTokenCommandHandler(
        IApiTokenRepository tokens,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILocalClock clock)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<CreatedApiTokenDto>> Handle(CreateApiTokenCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<CreatedApiTokenDto>(user);
        }

        try
        {
            var active = await _tokens.CountActiveForUserAsync(user.Value, cancellationToken);
            var issued = ApiToken.Issue(user.Value, request.Name, active, _clock.NowUtc());

            await _tokens.AddAsync(issued.Token, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(CreatedApiTokenDto.From(issued));
        }
        catch (DomainException ex)
        {
            return PlanAccess.FromDomain<CreatedApiTokenDto>(ex);
        }
    }
}
