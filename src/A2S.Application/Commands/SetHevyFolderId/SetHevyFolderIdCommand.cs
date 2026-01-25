using A2S.Application.Common;

namespace A2S.Application.Commands.SetHevyFolderId;

/// <summary>
/// Command to set the Hevy routine folder ID for a workout.
/// </summary>
public sealed record SetHevyFolderIdCommand(Guid WorkoutId, string FolderId) : ICommand<Result<bool>>;
