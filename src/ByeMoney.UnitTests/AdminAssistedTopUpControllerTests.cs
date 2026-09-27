using ByeMoney.API.Contracts.TopUp;
using ByeMoney.API.Controllers;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.CreateAdminCardToCardTopUp;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ByeMoney.UnitTests;

public class AdminAssistedTopUpControllerTests
{
    [Fact]
    public async Task Create_DelegatesReceiptCleanupOnConflict()
    {
        var actorId = Guid.NewGuid();
        var topUpService = new Mock<IAdminAssistedTopUpService>();
        var receipts = CreateReceipts();
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<CreateAdminCardToCardTopUpCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CreateTopUpRequestResponse>.Conflict(ApplicationErrors.TopUpRequest_IdempotencyConflict));
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(actorId);
        var controller = new AdminAssistedTopUpController(sender.Object, currentUser.Object, receipts.Object, topUpService.Object);

        var request = new AdminAssistedTopUpApiRequest("beneficiary", 100_000m, CreateFormFile(), null);
        var result = await controller.Create(request, "same-key", CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
        topUpService.Verify(x => x.DeleteUnreferencedReceiptAsync("receipt-id", "same-key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_DelegatesReceiptCleanupWhenBeneficiaryIsMissing()
    {
        var topUpService = new Mock<IAdminAssistedTopUpService>();
        var receipts = CreateReceipts();
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<CreateAdminCardToCardTopUpCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CreateTopUpRequestResponse>.NotFound(ApplicationErrors.TopUpRequest_InactiveBeneficiary));
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        var controller = new AdminAssistedTopUpController(sender.Object, currentUser.Object, receipts.Object, topUpService.Object);

        var request = new AdminAssistedTopUpApiRequest("beneficiary", 100_000m, CreateFormFile(), null);
        var result = await controller.Create(request, "same-key", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
        topUpService.Verify(x => x.DeleteUnreferencedReceiptAsync("receipt-id", "same-key", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IReceiptStorage> CreateReceipts()
    {
        var receipts = new Mock<IReceiptStorage>();
        receipts.Setup(x => x.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), "same-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync("receipt-id");
        return receipts;
    }

    private static IFormFile CreateFormFile()
    {
        var content = new MemoryStream(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        return new FormFile(content, 0, content.Length, "receipt", "receipt.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
    }
}

