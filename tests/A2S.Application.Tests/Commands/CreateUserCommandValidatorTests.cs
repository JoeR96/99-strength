using A2S.Application.Commands.Users;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace A2S.Application.Tests.Commands;

public class CreateUserCommandValidatorTests
{
    private readonly CreateUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldHaveNoErrors()
    {
        var command = new CreateUserCommand("test@example.com", "Test User");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyEmail_ShouldHaveError(string? email)
    {
        var command = new CreateUserCommand(email!, "Test User");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required.");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("invalid@")]
    [InlineData("@invalid.com")]
    public void Validate_WithInvalidEmailFormat_ShouldHaveError(string email)
    {
        var command = new CreateUserCommand(email, "Test User");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is not in a valid format.");
    }

    [Fact]
    public void Validate_WithEmailExceedingMaxLength_ShouldHaveError()
    {
        var longEmail = new string('a', 250) + "@example.com"; // 262 chars
        var command = new CreateUserCommand(longEmail, "Test User");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email must not exceed 256 characters.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyName_ShouldHaveError(string? name)
    {
        var command = new CreateUserCommand("test@example.com", name!);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name is required.");
    }

    [Fact]
    public void Validate_WithNameExceedingMaxLength_ShouldHaveError()
    {
        var longName = new string('a', 101);
        var command = new CreateUserCommand("test@example.com", longName);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name must not exceed 100 characters.");
    }
}
