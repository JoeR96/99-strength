using A2S.Application.DTOs;

namespace A2S.Application.Queries.GetExerciseLibrary;

/// <summary>
/// Response containing all exercise templates from the library.
/// </summary>
public sealed record ExerciseLibraryDto
{
    public IReadOnlyList<ExerciseTemplateDto> Templates { get; init; } = [];
}
