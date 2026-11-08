namespace A2S.Application.Common;

/// <summary>
/// The user a background job is acting for. Scoped: a job creates a DI scope per user, sets this, and
/// the API's <c>ICurrentUserService</c> falls back to it when there is no HTTP request, so command
/// handlers that ask "who is calling" (CompleteDay, CompleteSession) work unchanged from a job.
/// </summary>
public sealed class AmbientUser
{
    public string? UserId { get; set; }
}
