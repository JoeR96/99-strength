namespace A2S.Infrastructure.Persistence;

/// <summary>
/// Persistence model (not a domain concept) for a keyed write's first answer: user + key is unique.
/// See <see cref="A2S.Application.Idempotency.IIdempotencyStore"/>.
/// </summary>
public sealed class IdempotencyRecord
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
