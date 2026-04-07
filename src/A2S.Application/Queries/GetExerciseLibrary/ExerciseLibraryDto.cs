using A2S.Application.DTOs;

namespace A2S.Application.Queries.GetExerciseLibrary;

/// <summary>
/// Response containing exercise templates from the library with pagination.
/// </summary>
public sealed record ExerciseLibraryDto
{
    public IReadOnlyList<ExerciseTemplateDto> Templates { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
