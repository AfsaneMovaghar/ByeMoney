using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.History.Interfaces;
using ByeMoney.Application.Modules.Purchases.History.Services;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminCoursePurchaseReceipt;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminCoursePurchases;
using ByeMoney.Application.Modules.Purchases.Queries.GetMyCoursePurchaseReceipt;
using ByeMoney.Application.Modules.Purchases.Queries.GetMyCoursePurchases;
using System.Reflection;
using ByeMoney.API.Controllers;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Constants;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Moq;

namespace ByeMoney.UnitTests;

public sealed class PurchaseHistoryTests
{
    [Theory]
    [InlineData(CoursePurchaseStatus.Debited, "Pending")]
    [InlineData(CoursePurchaseStatus.NotificationFailed, "Pending")]
    [InlineData(CoursePurchaseStatus.NotificationSent, "Delivered")]
    public void FinancialSuccessDoesNotDependOnDelivery(CoursePurchaseStatus status, string delivery)
    {
        var purchase = PaidPurchase(80, 12000);
        if (status == CoursePurchaseStatus.NotificationSent)
        {
            purchase.MarkNotificationSent();
        }
        else if (status == CoursePurchaseStatus.NotificationFailed)
        {
            purchase.RecordNotificationFailure("سرویس تحویل در دسترس نیست.");
        }

        var receipt = PurchaseHistoryMapping.ToReceipt(new PurchaseReceiptData(purchase, null));

        receipt.FinancialStatus.Should().Be("Paid");
        receipt.DeliveryStatus.Should().Be(delivery);
        receipt.ReferenceRialPerNoorAtPurchaseTime.Should().Be(12000);
        receipt.FinalAmountInNoor.Should().Be(80);
    }

    [Fact]
    public void FreePurchaseHasPaidReceiptWithoutLedgerDebit()
    {
        var buyer = UserId.New();
        var cart = CourseCart.Create(Guid.NewGuid(), buyer.Value, "buyer", ["course"], "FREE");
        cart.SetQuote([new CourseCartQuoteItem("course", "دوره رایگان", 100, 0, 100, 0)]);
        var purchase = CoursePurchase.Create(buyer,
            ProductSnapshot.Create("course", "دوره رایگان", 0, isFree: true), isFree: true);
        purchase.AttachToCart(cart.Id);
        purchase.MarkDebited(cart.Id);
        cart.MarkPaid();

        var receipt = PurchaseHistoryMapping.ToReceipt(new PurchaseReceiptData(purchase, cart));

        receipt.FinancialStatus.Should().Be("Paid");
        receipt.DeliveryStatus.Should().Be("Pending");
        receipt.FinalAmountInNoor.Should().Be(0);
        receipt.BaseAmountInNoor.Should().Be(100);
        receipt.DiscountAmountInNoor.Should().Be(100);
        receipt.CouponCode.Should().Be("FREE");
        receipt.CartSummary!.CouponDiscountInNoor.Should().Be(100);
        receipt.ReferenceRialPerNoorAtPurchaseTime.Should().BeNull();
    }

    [Fact]
    public void OldPurchaseDoesNotInventRateOrDiscounts()
    {
        var purchase = PaidPurchase(42, null);
        typeof(ProductSnapshot).GetProperty(nameof(ProductSnapshot.ConversionRateAtPurchaseTime))!
            .SetValue(purchase.Snapshot, 9000m);
        var receipt = PurchaseHistoryMapping.ToReceipt(new PurchaseReceiptData(purchase, null));

        receipt.ReferenceRialPerNoorAtPurchaseTime.Should().BeNull();
        receipt.BaseAmountInNoor.Should().BeNull();
        receipt.DiscountAmountInNoor.Should().BeNull();
        receipt.CouponCode.Should().BeNull();
        receipt.CartSummary.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task InvalidPaginationIsRejected(int page, int size)
    {
        var result = await new GetMyCoursePurchasesQueryValidator()
            .ValidateAsync(new GetMyCoursePurchasesQuery(page, size));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task InvalidAdminFiltersAreRejected()
    {
        var now = DateTimeOffset.UtcNow;
        PurchaseHistoryFilter[] filters =
        [
            new(BuyerExternalUserId: " "),
            new(FromUtc: now, ToUtc: now),
            new(FinancialStatus: "Failed"),
            new(DeliveryStatus: "3")
        ];
        foreach (var filter in filters)
        {
            var result = await new GetAdminCoursePurchasesQueryValidator()
                .ValidateAsync(new GetAdminCoursePurchasesQuery(filter));
            result.IsValid.Should().BeFalse();
        }
    }

    [Fact]
    public async Task ReceiptUsesAuthenticatedOwnerAndReturnsNotFound()
    {
        var owner = Guid.NewGuid();
        var purchaseId = Guid.NewGuid();
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(x => x.UserId).Returns(owner);
        var reader = new Mock<IPurchaseHistoryReader>(MockBehavior.Strict);
        reader.Setup(x => x.GetReceiptAsync(purchaseId, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseReceiptData?)null);
        var handler = new GetMyCoursePurchaseReceiptQueryHandler(reader.Object, current.Object);

        var action = () => handler.Handle(new GetMyCoursePurchaseReceiptQuery(purchaseId), default);

        await action.Should().ThrowAsync<NotFoundException>().WithMessage("خرید پیدا نشد.");
        reader.VerifyAll();
    }

    [Fact]
    public async Task AdminReceiptReadsAnotherBuyersPurchase()
    {
        var purchase = PaidPurchase(80, 12000);
        var adminId = Guid.NewGuid();
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(x => x.UserId).Returns(adminId);
        var reader = new Mock<IPurchaseHistoryReader>(MockBehavior.Strict);
        reader.Setup(x => x.GetAdminReceiptAsync(purchase.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseReceiptData(purchase, null));
        var handler = new GetAdminCoursePurchaseReceiptQueryHandler(reader.Object, current.Object);

        var receipt = await handler.Handle(new GetAdminCoursePurchaseReceiptQuery(purchase.Id.Value), default);

        purchase.BuyerId.Value.Should().NotBe(adminId);
        receipt.PurchaseId.Should().Be(purchase.Id.Value);
        receipt.FinalAmountInNoor.Should().Be(80);
        reader.VerifyAll();
    }

    [Fact]
    public async Task AdminReceiptReturnsNotFoundForMissingPurchase()
    {
        var purchaseId = Guid.NewGuid();
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        var reader = new Mock<IPurchaseHistoryReader>(MockBehavior.Strict);
        reader.Setup(x => x.GetAdminReceiptAsync(purchaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseReceiptData?)null);
        var handler = new GetAdminCoursePurchaseReceiptQueryHandler(reader.Object, current.Object);

        var action = () => handler.Handle(new GetAdminCoursePurchaseReceiptQuery(purchaseId), default);

        await action.Should().ThrowAsync<NotFoundException>();
        reader.VerifyAll();
    }

    [Fact]
    public async Task AdminReceiptWithoutInternalIdentityDoesNotQueryPurchases()
    {
        var reader = new Mock<IPurchaseHistoryReader>(MockBehavior.Strict);
        var handler = new GetAdminCoursePurchaseReceiptQueryHandler(reader.Object, Mock.Of<ICurrentUserService>());

        var action = () => handler.Handle(new GetAdminCoursePurchaseReceiptQuery(Guid.NewGuid()), default);

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        reader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AdminReceiptRejectsEmptyPurchaseId()
    {
        var result = await new GetAdminCoursePurchaseReceiptQueryValidator()
            .ValidateAsync(new GetAdminCoursePurchaseReceiptQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task MissingInternalIdentityDoesNotQueryPurchases()
    {
        var reader = new Mock<IPurchaseHistoryReader>(MockBehavior.Strict);
        var handler = new GetMyCoursePurchasesQueryHandler(reader.Object, Mock.Of<ICurrentUserService>());

        var action = () => handler.Handle(new GetMyCoursePurchasesQuery(), default);

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        reader.VerifyNoOtherCalls();
    }

    [Fact]
    public void AdminRequiresDedicatedReadPermissionAndUserEndpointsRequireAuthentication()
    {
        typeof(AdminCoursePurchasesController).GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(PolicyNames.RequirePurchasesView);
        typeof(MyCoursePurchasesController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
    }

    private static CoursePurchase PaidPurchase(decimal amount, decimal? rate)
    {
        var purchase = CoursePurchase.Create(UserId.New(),
            ProductSnapshot.Create("course", "عنوان تاریخی دوره", amount,
                referenceRialPerNoorAtPurchaseTime: rate));
        purchase.MarkDebited(Guid.NewGuid());
        return purchase;
    }
}
