using A2S.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Entities;

public class UserTests
{
    [Fact]
    public void Create_WithValidInput_ShouldCreateUser()
    {
        // Arrange
        var email = "test@example.com";
        var name = "Test User";

        // Act
        var user = User.Create(email, name);

        // Assert
        user.Should().NotBeNull();
        user.Id.Should().NotBeEmpty();
        user.Email.Should().Be("test@example.com");
        user.Name.Should().Be("Test User");
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithMixedCaseEmail_ShouldNormalizeToLowercase()
    {
        // Arrange
        var email = "Test@EXAMPLE.com";
        var name = "Test User";

        // Act
        var user = User.Create(email, name);

        // Assert
        user.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void Create_WithWhitespaceInName_ShouldTrim()
    {
        // Arrange
        var email = "test@example.com";
        var name = "  Test User  ";

        // Act
        var user = User.Create(email, name);

        // Assert
        user.Name.Should().Be("Test User");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrEmptyEmail_ShouldThrowArgumentException(string? email)
    {
        // Arrange
        var name = "Test User";

        // Act
        var act = () => User.Create(email!, name);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Email cannot be null or empty*");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("invalid@")]
    [InlineData("@invalid.com")]
    [InlineData("invalid@.com")]
    [InlineData("invalid@com")]
    public void Create_WithInvalidEmailFormat_ShouldThrowArgumentException(string email)
    {
        // Arrange
        var name = "Test User";

        // Act
        var act = () => User.Create(email, name);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Email is not in a valid format*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrEmptyName_ShouldThrowArgumentException(string? name)
    {
        // Arrange
        var email = "test@example.com";

        // Act
        var act = () => User.Create(email, name!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Name cannot be null or empty*");
    }

    [Fact]
    public void UpdateName_WithValidName_ShouldUpdateName()
    {
        // Arrange
        var user = User.Create("test@example.com", "Original Name");

        // Act
        user.UpdateName("New Name");

        // Assert
        user.Name.Should().Be("New Name");
    }

    [Fact]
    public void UpdateName_WithNullOrEmptyName_ShouldThrowArgumentException()
    {
        // Arrange
        var user = User.Create("test@example.com", "Original Name");

        // Act
        var act = () => user.UpdateName("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Name cannot be null or empty*");
    }

    [Fact]
    public void Reconstitute_ShouldCreateUserWithGivenValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = "test@example.com";
        var name = "Test User";
        var createdAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var user = User.Reconstitute(id, email, name, createdAt);

        // Assert
        user.Id.Should().Be(id);
        user.Email.Should().Be(email);
        user.Name.Should().Be(name);
        user.CreatedAt.Should().Be(createdAt);
    }
}
