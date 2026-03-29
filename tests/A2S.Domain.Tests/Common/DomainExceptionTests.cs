using A2S.Domain.Common;
using FluentAssertions;
using Xunit;

namespace A2S.Domain.Tests.Common;

public class DomainExceptionTests
{
    [Fact]
    public void DomainException_WhenCreatedWithMessage_ShouldSetMessage()
    {
        var exception = new DomainException("test error");

        exception.Message.Should().Be("test error");
    }

    [Fact]
    public void DomainException_WhenCreatedWithInnerException_ShouldSetInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var exception = new DomainException("outer", inner);

        exception.Message.Should().Be("outer");
        exception.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void BusinessRuleViolationException_ShouldBeDomainException()
    {
        var exception = new BusinessRuleViolationException("rule violated");

        exception.Should().BeAssignableTo<DomainException>();
        exception.Message.Should().Be("rule violated");
    }

    [Fact]
    public void EntityNotFoundException_WhenCreatedWithTypeAndId_ShouldFormatMessage()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var exception = new EntityNotFoundException("Workout", id);

        exception.Message.Should().Be("Workout with ID '11111111-1111-1111-1111-111111111111' was not found.");
        exception.EntityType.Should().Be("Workout");
        exception.EntityId.Should().Be(id);
        exception.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void EntityNotFoundException_WhenCreatedWithMessage_ShouldSetMessage()
    {
        var exception = new EntityNotFoundException("not found");

        exception.Message.Should().Be("not found");
        exception.EntityType.Should().BeEmpty();
    }

    [Fact]
    public void AuthorizationException_ShouldBeDomainException()
    {
        var exception = new AuthorizationException("not allowed");

        exception.Should().BeAssignableTo<DomainException>();
        exception.Message.Should().Be("not allowed");
    }

    [Fact]
    public void ConcurrencyException_ShouldBeDomainException()
    {
        var exception = new ConcurrencyException("conflict");

        exception.Should().BeAssignableTo<DomainException>();
        exception.Message.Should().Be("conflict");
    }

    [Fact]
    public void ConcurrencyException_WhenCreatedWithInnerException_ShouldSetInnerException()
    {
        var inner = new InvalidOperationException("db conflict");
        var exception = new ConcurrencyException("conflict", inner);

        exception.InnerException.Should().BeSameAs(inner);
    }
}
