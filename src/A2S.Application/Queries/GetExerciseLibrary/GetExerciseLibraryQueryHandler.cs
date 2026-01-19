using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Services;
using MediatR;

namespace A2S.Application.Queries.GetExerciseLibrary;

/// <summary>
/// Handler for GetExerciseLibraryQuery.
/// Returns the predefined exercise library.
/// </summary>
public sealed class GetExerciseLibraryQueryHandler : IRequestHandler<GetExerciseLibraryQuery, Result<ExerciseLibraryDto>>
{
    public Task<Result<ExerciseLibraryDto>> Handle(GetExerciseLibraryQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var templates = ExerciseLibrary.AllTemplates.Select(MapToDto).ToList();

            var result = new ExerciseLibraryDto
            {
                Templates = templates
            };

            return Task.FromResult(Result.Success(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure<ExerciseLibraryDto>($"Failed to retrieve exercise library: {ex.Message}"));
        }
    }

    private static ExerciseTemplateDto MapToDto(ExerciseLibrary.ExerciseTemplate template)
    {
        return new ExerciseTemplateDto
        {
            Name = template.Name,
            Equipment = template.Equipment,
            DefaultRepRange = template.DefaultRepRange != null
                ? new RepRangeDto
                {
                    Minimum = template.DefaultRepRange.Minimum,
                    Target = template.DefaultRepRange.Target,
                    Maximum = template.DefaultRepRange.Maximum
                }
                : null,
            DefaultSets = template.DefaultSets,
            Description = template.Description
        };
    }
}
