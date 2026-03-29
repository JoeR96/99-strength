using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2S.Api.Controllers;

/// <summary>
/// API controller for workout Hevy sync operations.
/// </summary>
[ApiController]
[Route("api/v1/workouts")]
[Authorize]
public class WorkoutSyncController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WorkoutSyncController> _logger;

    public WorkoutSyncController(IMediator mediator, ILogger<WorkoutSyncController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
