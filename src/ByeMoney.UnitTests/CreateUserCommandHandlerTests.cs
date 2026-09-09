using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Commands.CreateUser;
using ByeMoney.Domain.Modules.Identity.Users;
using FluentAssertions;
using Moq;
using Xunit;

namespace ByeMoney.UnitTests;

public class CreateUserCommandHandlerTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public async Task Handle_ShouldCalculateIsActiveCorrectly_BasedOnConfirmedAndBlocked(
        bool confirmed, bool blocked, bool expectedIsActive)
    {
        // Arrange
        var userRepositoryMock = new Mock<IRepository<User, UserId>>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        User? capturedUser = null;
        userRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => capturedUser = u);

        var handler = new CreateUserCommandHandler(userRepositoryMock.Object, unitOfWorkMock.Object);
        var command = new CreateUserCommand(
            ExternalUserId: "doc-123",
            FirstName: "Ali",
            LastName: "Rezaei",
            Phone: "09123456789",
            Email: "ali@example.com",
            Confirmed: confirmed,
            Blocked: blocked);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        capturedUser.Should().NotBeNull();
        capturedUser!.IsActive.Should().Be(expectedIsActive);
        capturedUser.ExternalUserId.Should().Be("doc-123");
        userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
