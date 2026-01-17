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
        // Arrange
        var command = new CreateUserCommand("test@example.com", "Test User");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyEmail_ShouldHaveError(string? email)
    {
        // Arrange
        var command = new CreateUserCommand(email!, "Test User");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required.");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("invalid@")]
    [InlineData("@invalid.com")]
    public void Validate_WithInvalidEmailFormat_ShouldHaveError(string email)
    {
        // Arrange
        var command = new CreateUserCommand(email, "Test User");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is not in a valid format.");
    }

    [Fact]
    public void Validate_WithEmailExceedingMaxLength_ShouldHaveError()
    {
        // Arrange
        var longEmail = new string('a', 250) + "@example.com"; // 262 chars
        var command = new CreateUserCommand(longEmail, "Test User");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email must not exceed 256 characters.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyName_ShouldHaveError(string? name)
    {
        // Arrange
        var command = new CreateUserCommand("test@example.com", name!);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name is required.");
    }

    [Fact]
    public void Validate_WithNameExceedingMaxLength_ShouldHaveError()
    {
        // Arrange
        var longName = new string('a', 101);
        var command = new CreateUserCommand("test@example.com", longName);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name must not exceed 100 characters.");
    }
}
