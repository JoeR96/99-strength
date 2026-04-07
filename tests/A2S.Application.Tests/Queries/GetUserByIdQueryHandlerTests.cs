using A2S.Application.Queries.Users;
using A2S.Domain.Common;
using A2S.Domain.Aggregates.User;
using A2S.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Queries;

public class GetUserByIdQueryHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly GetUserByIdQueryHandler _handler;

    public GetUserByIdQueryHandlerTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _handler = new GetUserByIdQueryHandler(_userRepository);
    }

    [Fact]
    public async Task Handle_WhenUserExists_ShouldReturnUserDto()
    {
        var userId = new UserId(Guid.NewGuid().ToString());
        var user = User.Reconstitute(userId.Value, "test@example.com", "Test User", DateTime.UtcNow);
        var query = new GetUserByIdQuery(userId);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(userId.Value);
        result.Email.Should().Be("test@example.com");
        result.Name.Should().Be("Test User");
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ShouldReturnNull()
    {
        var userId = new UserId(Guid.NewGuid().ToString());
        var query = new GetUserByIdQuery(userId);

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeNull();
    }
}
