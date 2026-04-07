using FluentValidation;

namespace A2S.Application.Queries.GetExerciseLibrary;

public sealed class GetExerciseLibraryQueryValidator : AbstractValidator<GetExerciseLibraryQuery>
{
    public GetExerciseLibraryQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
