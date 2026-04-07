using A2S.Application.Services;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Memory;

namespace A2S.Infrastructure.SeedData;

/// <summary>
/// Provides exercise templates from the database via IExerciseDefinitionRepository.
/// Caches results in memory with a sliding expiration.
/// </summary>
public sealed class ExerciseLibraryProvider : IExerciseLibraryProvider
{
    private const string CacheKey = "ExerciseLibrary_AllTemplates";
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(5);

    private readonly IExerciseDefinitionRepository _repository;
    private readonly IMemoryCache _cache;

    public ExerciseLibraryProvider(IExerciseDefinitionRepository repository, IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public IReadOnlyList<ExerciseTemplate> AllTemplates =>
        _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.SlidingExpiration = SlidingExpiration;
            return LoadTemplatesFromDbAsync().GetAwaiter().GetResult();
        })!;

    public async Task<IReadOnlyList<ExerciseTemplate>> GetAllTemplatesAsync(CancellationToken ct = default)
    {
        var cached = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.SlidingExpiration = SlidingExpiration;
            return await LoadTemplatesFromDbAsync(ct);
        });
        return cached!;
    }

    public ExerciseTemplate? GetByName(string name) =>
        AllTemplates.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private async Task<IReadOnlyList<ExerciseTemplate>> LoadTemplatesFromDbAsync(CancellationToken ct = default)
    {
        var definitions = await _repository.GetAllAsync(ct);

        return definitions.Select(d => new ExerciseTemplate(
            Name: d.Name,
            Equipment: d.EquipmentType,
            DefaultRepRange: d.DefaultRepRangeMin.HasValue && d.DefaultRepRangeMax.HasValue
                ? RepRange.Create(d.DefaultRepRangeMin.Value, d.DefaultRepRangeMax.Value)
                : null,
            DefaultSets: d.DefaultSets,
            Description: d.Description
        )).ToArray();
    }
}
