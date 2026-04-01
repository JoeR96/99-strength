using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.GetExerciseLibrary;

/// <summary>
/// Handler for GetExerciseLibraryQuery.
/// Returns exercise definitions from the database.
/// </summary>
public sealed class GetExerciseLibraryQueryHandler : IRequestHandler<GetExerciseLibraryQuery, Result<ExerciseLibraryDto>>
{
    private readonly IExerciseDefinitionRepository _exerciseDefinitionRepository;

    public GetExerciseLibraryQueryHandler(IExerciseDefinitionRepository exerciseDefinitionRepository)
    {
        _exerciseDefinitionRepository = exerciseDefinitionRepository;
    }

    public async Task<Result<ExerciseLibraryDto>> Handle(GetExerciseLibraryQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var hasFilters = request.EquipmentType.HasValue
                || !string.IsNullOrWhiteSpace(request.MuscleGroup)
                || !string.IsNullOrWhiteSpace(request.SearchTerm)
                || !string.IsNullOrWhiteSpace(request.Category);

            var definitions = hasFilters
                ? await _exerciseDefinitionRepository.SearchAsync(
                    request.EquipmentType,
                    request.MuscleGroup ?? request.Category,
                    request.SearchTerm,
                    cancellationToken)
                : await _exerciseDefinitionRepository.GetAllAsync(cancellationToken);

            var templates = definitions.Select(d => new ExerciseTemplateDto
            {
                Name = d.Name,
                Equipment = d.EquipmentType,
                DefaultRepRange = d.DefaultRepRangeMin.HasValue && d.DefaultRepRangeMax.HasValue
                    ? new RepRangeDto
                    {
                        Minimum = d.DefaultRepRangeMin.Value,
                        Maximum = d.DefaultRepRangeMax.Value
                    }
                    : null,
                DefaultSets = d.DefaultSets,
                Description = d.Description
            }).ToList();

            var result = new ExerciseLibraryDto
            {
                Templates = templates
            };

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            return Result.Failure<ExerciseLibraryDto>($"Failed to retrieve exercise library: {ex.Message}");
        }
    }
}
