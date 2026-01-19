using A2S.Application.Common;
using A2S.Application.DTOs;
using MediatR;

namespace A2S.Application.Queries.GetExerciseLibrary;

/// <summary>
/// Query to get all available exercises from the library.
/// </summary>
public sealed record GetExerciseLibraryQuery : IRequest<Result<ExerciseLibraryDto>>
{
    /// <summary>
    /// Optional filter by category.
    /// </summary>
    public string? Category { get; init; }
}

/// <summary>
/// Response containing all exercise templates from the library.
/// Templates contain only exercise metadata - users configure
/// category, progression, day, and order when adding to workout.
/// </summary>
public sealed record ExerciseLibraryDto
{
    /// <summary>
    /// All available exercise templates.
    /// </summary>
    public IReadOnlyList<ExerciseTemplateDto> Templates { get; init; } = Array.Empty<ExerciseTemplateDto>();
}
