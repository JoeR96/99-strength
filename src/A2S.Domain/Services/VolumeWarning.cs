namespace A2S.Domain.Services;

/// <summary>
/// A muscle-group bucket whose planned hard sets for the week exceed its cap.
/// Warnings inform; they never block a change.
/// </summary>
public sealed record VolumeWarning(VolumeBucket Bucket, int Sets, int Cap, string Message);
