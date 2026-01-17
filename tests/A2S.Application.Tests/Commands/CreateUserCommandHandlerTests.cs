using A2S.Application.Commands.Users;
using A2S.Domain.Entities;
using A2S.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands;

public class CreateUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly CreateUserCommandHandler _handler;

    public CreateUserCommandHandlerTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _handler = new CreateUserCommandHandler(_userRepository);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateUser()
    {
        // Arrange
        var command = new CreateUserCommand("test@example.com", "Test User");
        _userRepository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("test@example.com");
        result.Name.Should().Be("Test User");
        result.Id.Should().NotBeEmpty();

        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var existingUser = User.Create("existing@example.com", "Existing User");
        var command = new CreateUserCommand("existing@example.com", "New User");

        _userRepository.GetByEmailAsync("existing@example.com", Arg.Any<CancellationToken>())
            .Returns(existingUser);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");

        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
