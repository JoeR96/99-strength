using A2S.Domain.Entities;
using A2S.Domain.Enums;

namespace A2S.Domain.Repositories;

/// <summary>
/// Repository interface for ExerciseDefinition reference data.
/// </summary>
public interface IExerciseDefinitionRepository
{
    Task<IReadOnlyList<ExerciseDefinition>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ExerciseDefinition>> SearchAsync(
        EquipmentType? equipmentType = null,
        string? muscleGroup = null,
        string? searchTerm = null,
        CancellationToken ct = default);

    Task<(IReadOnlyList<ExerciseDefinition> Items, int TotalCount)> SearchPagedAsync(
        EquipmentType? equipmentType = null,
        string? muscleGroup = null,
        string? searchTerm = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default);

    Task<ExerciseDefinition?> GetByNameAsync(string name, CancellationToken ct = default);
}
