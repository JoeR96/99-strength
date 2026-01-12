using FluentValidation.Results;

namespace A2S.Application.Exceptions;

/// <summary>
/// Exception thrown when FluentValidation validation fails in the MediatR pipeline.
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>
    /// Dictionary of validation errors where key is the property name and value is an array of error messages.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException()
        : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : this()
    {
        Errors = failures
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray());
    }
}
