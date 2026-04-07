using A2S.Domain.Entities;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace A2S.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for ExerciseDefinition reference data.
/// </summary>
public sealed class ExerciseDefinitionRepository : IExerciseDefinitionRepository
{
    private readonly A2SDbContext _context;

    public ExerciseDefinitionRepository(A2SDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ExerciseDefinition>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.ExerciseDefinitions
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ExerciseDefinition>> SearchAsync(
        EquipmentType? equipmentType = null,
        string? muscleGroup = null,
        string? searchTerm = null,
        CancellationToken ct = default)
    {
        var query = _context.ExerciseDefinitions.AsNoTracking();

        if (equipmentType.HasValue)
        {
            query = query.Where(e => e.EquipmentType == equipmentType.Value);
        }

        if (!string.IsNullOrWhiteSpace(muscleGroup))
        {
            query = query.Where(e => e.MuscleGroup == muscleGroup);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var escaped = EscapeLikeWildcards(searchTerm);
            query = query.Where(e => EF.Functions.ILike(e.Name, $"%{escaped}%"));
        }

        return await query.OrderBy(e => e.Name).ToListAsync(ct);
    }

    public async Task<ExerciseDefinition?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await _context.ExerciseDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Name == name, ct);
    }

    public async Task<(IReadOnlyList<ExerciseDefinition> Items, int TotalCount)> SearchPagedAsync(
        EquipmentType? equipmentType = null,
        string? muscleGroup = null,
        string? searchTerm = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = _context.ExerciseDefinitions.AsNoTracking();

        if (equipmentType.HasValue)
        {
            query = query.Where(e => e.EquipmentType == equipmentType.Value);
        }

        if (!string.IsNullOrWhiteSpace(muscleGroup))
        {
            query = query.Where(e => e.MuscleGroup == muscleGroup);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var escaped = EscapeLikeWildcards(searchTerm);
            query = query.Where(e => EF.Functions.ILike(e.Name, $"%{escaped}%"));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(e => e.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    private static string EscapeLikeWildcards(string input) =>
        input.Replace("%", "\\%").Replace("_", "\\_");
}
