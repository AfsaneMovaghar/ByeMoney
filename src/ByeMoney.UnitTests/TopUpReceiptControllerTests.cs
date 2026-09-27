using System.Security.Claims;
using ByeMoney.API.Controllers;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Identity.Constants;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ByeMoney.UnitTests;

public class TopUpReceiptControllerTests
{
    [Fact]
    public async Task Get_ReturnsNotFound_WhenTopUpHasNoReceipt()
    {
        var topUps = new Mock<ITopUpRequestRepository>();
        topUps.Setup(x => x.GetByIdAsync(It.IsAny<TopUpRequestId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TopUpRequest?)null);

        var controller = new TopUpReceiptController(
            topUps.Object, Mock.Of<IReceiptStorage>(), Mock.Of<ICurrentUserService>(), Mock.Of<IAuthorizationService>());

        var result = await controller.Get(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenUserIsNotBeneficiaryAndNotAuthorizedAdmin()
    {
        var beneficiaryId = UserId.New();
        var topUp = TopUpRequest.CreateAdminCardToCard(beneficiaryId, UserId.New(), 100_000m, 10_000m, "receipt.png", "key");
        var topUps = new Mock<ITopUpRequestRepository>();
        topUps.Setup(x => x.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>())).ReturnsAsync(topUp);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());

        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(x => x.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), PolicyNames.RequireTopUpReview))
            .ReturnsAsync(AuthorizationResult.Failed());

        var controller = new TopUpReceiptController(topUps.Object, Mock.Of<IReceiptStorage>(), currentUser.Object, authorization.Object);

        var result = await controller.Get(topUp.Id.Value, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Get_ReturnsFile_WhenUserIsBeneficiary()
    {
        var beneficiaryId = UserId.New();
        var topUp = TopUpRequest.CreateAdminCardToCard(beneficiaryId, UserId.New(), 100_000m, 10_000m, "receipt.png", "key");
        var topUps = new Mock<ITopUpRequestRepository>();
        topUps.Setup(x => x.GetByIdAsync(topUp.Id, It.IsAny<CancellationToken>())).ReturnsAsync(topUp);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(beneficiaryId.Value);

        var receipts = new Mock<IReceiptStorage>();
        receipts.Setup(x => x.OpenReadAsync("receipt.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));

        var controller = new TopUpReceiptController(
            topUps.Object, receipts.Object, currentUser.Object, Mock.Of<IAuthorizationService>());

        var result = await controller.Get(topUp.Id.Value, CancellationToken.None);

        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("image/png");
    }
}

