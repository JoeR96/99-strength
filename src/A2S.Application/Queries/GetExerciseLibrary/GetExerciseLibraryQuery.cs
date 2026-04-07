using A2S.Application.Common;
using A2S.Domain.Enums;

namespace A2S.Application.Queries.GetExerciseLibrary;

/// <summary>
/// Query to get exercises from the library with optional filters and pagination.
/// </summary>
public sealed record GetExerciseLibraryQuery : IQuery<Result<ExerciseLibraryDto>>
{
    /// <summary>
    /// Optional filter by equipment type.
    /// </summary>
    public EquipmentType? EquipmentType { get; init; }

    /// <summary>
    /// Optional filter by muscle group (e.g., "biceps", "chest").
    /// </summary>
    public string? MuscleGroup { get; init; }

    /// <summary>
    /// Optional search term to filter by exercise name.
    /// </summary>
    public string? SearchTerm { get; init; }

    /// <summary>
    /// Optional filter by category (deprecated - use MuscleGroup instead).
    /// </summary>
    [Obsolete("Use MuscleGroup instead")]
    public string? Category { get; init; }

    /// <summary>
    /// Page number (1-based). Defaults to 1.
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Page size. Defaults to 50.
    /// </summary>
    public int PageSize { get; init; } = 50;
}
