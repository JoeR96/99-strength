using A2S.Application.Common;
using A2S.Application.Hevy;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.GymSessions;

/// <summary>"Retry" on Today's "Not in Hevy yet": makes a gym session's Hevy post due now and tries it once.</summary>
public sealed record RetryHevyPostCommand(Guid ClientSessionId) : ICommand<Result<HevyPostStatusDto>>;

public sealed record HevyPostStatusDto(Guid ClientSessionId, string Status, string? HevyWorkoutId, int Attempts, string? LastError);

public sealed class RetryHevyPostCommandHandler : IRequestHandler<RetryHevyPostCommand, Result<HevyPostStatusDto>>
{
    private readonly IHevyWorkoutPostRepository _posts;
    private readonly HevyWorkoutPoster _poster;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly Domain.Common.ILocalClock _clock;

    public RetryHevyPostCommandHandler(
        IHevyWorkoutPostRepository posts,
        HevyWorkoutPoster poster,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        Domain.Common.ILocalClock clock)
    {
        _posts = posts ?? throw new ArgumentNullException(nameof(posts));
        _poster = poster ?? throw new ArgumentNullException(nameof(poster));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<HevyPostStatusDto>> Handle(RetryHevyPostCommand request, CancellationToken cancellationToken)
    {
        var user = PlanAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return PlanAccess.Fail<HevyPostStatusDto>(user);
        }

        var post = await _posts.FindByClientSessionAsync(user.Value, request.ClientSessionId, cancellationToken);
        if (post is null)
        {
            return Result.Failure<HevyPostStatusDto>("Gym session not found.", ErrorCode.NotFound);
        }

        post.RequeueNow(_clock.NowUtc());
        _posts.Update(post);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _poster.PostAsync(post, cancellationToken);

        return Result.Success(new HevyPostStatusDto(post.ClientSessionId, post.Status.ToString(), post.HevyWorkoutId, post.Attempts, post.LastError));
    }
}
