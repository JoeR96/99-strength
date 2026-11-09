using A2S.Application.Common;
using A2S.Application.DTOs;
using FluentValidation;

namespace A2S.Application.Commands.Push;

/// <summary>
/// Stores (or refreshes) the calling browser's push subscription. Matched on endpoint, so subscribing
/// again from the same device updates the keys and the nudge time instead of adding a row.
/// </summary>
public sealed record SubscribePushCommand(
    string Endpoint,
    string P256dh,
    string Auth,
    string? UserAgent,
    TimeOnly? SessionStartTime) : ICommand<Result<PushSubscriptionDto>>;

public sealed class SubscribePushCommandValidator : AbstractValidator<SubscribePushCommand>
{
    public SubscribePushCommandValidator()
    {
        RuleFor(x => x.Endpoint).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.P256dh).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Auth).NotEmpty().MaximumLength(256);
    }
}
