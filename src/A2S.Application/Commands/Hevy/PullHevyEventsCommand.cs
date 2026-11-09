using A2S.Application.Common;
using A2S.Application.Hevy;
using MediatR;

namespace A2S.Application.Commands.Hevy;

/// <summary>
/// Pulls the current user's Hevy workout events since their cursor and completes matched sessions.
/// <paramref name="Force"/> re-walks everything from the plan start.
/// </summary>
public sealed record PullHevyEventsCommand(bool Force = false) : ICommand<Result<PullSummary>>;

public sealed class PullHevyEventsCommandHandler : IRequestHandler<PullHevyEventsCommand, Result<PullSummary>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly HevyPullService _pull;

    public PullHevyEventsCommandHandler(ICurrentUserService currentUser, HevyPullService pull)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _pull = pull ?? throw new ArgumentNullException(nameof(pull));
    }

    public async Task<Result<PullSummary>> Handle(PullHevyEventsCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<PullSummary>(user);
        }

        return await _pull.PullAsync(user.Value, request.Force, cancellationToken);
    }
}
