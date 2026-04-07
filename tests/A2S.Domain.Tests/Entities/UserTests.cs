using A2S.Domain.Common;
using A2S.Domain.Aggregates.User;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Entities;

public class UserTests
{
    [Fact]
    public void Create_WithValidInput_ShouldCreateUser()
    {
        var email = "test@example.com";
        var name = "Test User";

        var user = User.Create(email, name);

        user.Should().NotBeNull();
        user.Id.Value.Should().NotBeEmpty();
        user.Email.Should().Be("test@example.com");
        user.Name.Should().Be("Test User");
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithMixedCaseEmail_ShouldNormalizeToLowercase()
    {
        var email = "Test@EXAMPLE.com";
        var name = "Test User";

        var user = User.Create(email, name);

        user.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void Create_WithWhitespaceInName_ShouldTrim()
    {
        var email = "test@example.com";
        var name = "  Test User  ";

        var user = User.Create(email, name);

        user.Name.Should().Be("Test User");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrEmptyEmail_ShouldThrowArgumentException(string? email)
    {
        var name = "Test User";

        var act = () => User.Create(email!, name);

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
        var name = "Test User";

        var act = () => User.Create(email, name);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Email is not in a valid format*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrEmptyName_ShouldThrowArgumentException(string? name)
    {
        var email = "test@example.com";

        var act = () => User.Create(email, name!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Name cannot be null or empty*");
    }

    [Fact]
    public void UpdateName_WithValidName_ShouldUpdateName()
    {
        var user = User.Create("test@example.com", "Original Name");

        user.UpdateName("New Name");

        user.Name.Should().Be("New Name");
    }

    [Fact]
    public void UpdateName_WithNullOrEmptyName_ShouldThrowArgumentException()
    {
        var user = User.Create("test@example.com", "Original Name");

        var act = () => user.UpdateName("");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Name cannot be null or empty*");
    }

    [Fact]
    public void Reconstitute_ShouldCreateUserWithGivenValues()
    {
        var id = Guid.NewGuid().ToString();
        var email = "test@example.com";
        var name = "Test User";
        var createdAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var user = User.Reconstitute(id, email, name, createdAt);

        user.Id.Should().Be(new UserId(id));
        user.Email.Should().Be(email);
        user.Name.Should().Be(name);
        user.CreatedAt.Should().Be(createdAt);
    }
}
