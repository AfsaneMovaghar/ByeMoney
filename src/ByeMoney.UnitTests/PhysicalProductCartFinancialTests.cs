using ByeMoney.Application.Modules.Purchases.Cart.DTOs;
using ByeMoney.Application.Modules.Purchases.Cart.Services;
using ByeMoney.Application.Modules.Purchases.Commands.PrepareCourseCart;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using FluentAssertions;

namespace ByeMoney.UnitTests;

public sealed class PhysicalProductCartFinancialTests
{
    [Fact]
    public void MixedCartPaymentWithQuantityAndShipping_BalancesLedgerAndDebitsExactTotal()
    {
        var buyer = new UserId(Guid.NewGuid());
        var userAccount = Account.CreateUserAccount(buyer);
        var wallet = Wallet.Create(userAccount.Id, buyer);
        wallet.ApplyCredit(1000m);
        var systemAccount = Account.CreateSystemAccount();

        var cartId = Guid.NewGuid();
        var cart = CourseCart.Create(cartId, buyer.Value, "buyer-ext-1", [
            new CartItemInput("course-1", ProductItemType.Course, 1),
            new CartItemInput("product-1", ProductItemType.Product, 2)
        ], "DISCOUNT");

        // Course: unit 100, itemDiscount 10, couponDiscount 10 => paid 80
        // Product: unit 50, qty 2 => lineTotal 100, itemDiscount 20, couponDiscount 10 => paid 70
        // Shipping: 25
        // OriginalTotal: 100 + 2 * 50 = 200
        // ItemDiscount: 30, CouponDiscount: 20, Shipping: 25
        // Cart.Total: 200 - 30 - 20 + 25 = 175
        // Allocations: 80 + 70 = 150. Allocations (150) + Shipping (25) == 175
        cart.SetQuote([
            new CourseCartQuoteItem("course-1", "دوره آموزشی", 100m, 10m, 10m, 80m, ProductItemType.Course, null, 1),
            new CourseCartQuoteItem("product-1", "کتاب فیزیکی", 50m, 20m, 10m, 70m, ProductItemType.Product, null, 2)
        ], shippingAmountInNoor: 25m);

        cart.OriginalTotal.Should().Be(200m);
        cart.Total.Should().Be(175m);
        cart.ShippingAmountInNoor.Should().Be(25m);

        var payment = CourseCartPayment.Apply(cart, new ProvisionedUserWallet(userAccount, wallet), systemAccount);

        wallet.Balance.Should().Be(825m);
        cart.Status.Should().Be(CourseCartStatus.Paid);

        // Verify CoursePurchase
        payment.Purchases.Should().HaveCount(1);
        var coursePurchase = payment.Purchases.Single();
        coursePurchase.Snapshot.ExternalProductId.Should().Be("course-1");
        coursePurchase.Snapshot.PriceInNoorAtPurchaseTime.Should().Be(80m);
        coursePurchase.CartId.Should().Be(cartId);

        // Verify PhysicalProductPurchase
        payment.PhysicalPurchases.Should().HaveCount(1);
        var physicalPurchase = payment.PhysicalPurchases.Single();
        physicalPurchase.ExternalProductId.Should().Be("product-1");
        physicalPurchase.ProductTitle.Should().Be("کتاب فیزیکی");
        physicalPurchase.Quantity.Should().Be(2);
        physicalPurchase.UnitPriceInNoor.Should().Be(50m);
        physicalPurchase.PaidAmountInNoor.Should().Be(70m);
        physicalPurchase.CartId.Should().Be(cartId);
        physicalPurchase.LedgerTransactionId.Should().Be(cartId);

        // Verify balanced single double-entry Ledger transaction
        payment.LedgerEntries.Should().HaveCount(2);
        payment.LedgerEntries.Sum(e => e.Amount).Should().Be(0m);
        payment.LedgerEntries.Single(e => e.Amount < 0).Amount.Should().Be(-175m);
        payment.LedgerEntries.Single(e => e.Amount > 0).Amount.Should().Be(175m);
        payment.LedgerEntries.Should().OnlyContain(e => e.TransactionId == cartId);
    }

    [Fact]
    public void FreeItemsWithPositiveShipping_IsNotFreeAndDebitsShippingAmount()
    {
        var buyer = new UserId(Guid.NewGuid());
        var userAccount = Account.CreateUserAccount(buyer);
        var wallet = Wallet.Create(userAccount.Id, buyer);
        wallet.ApplyCredit(100m);
        var systemAccount = Account.CreateSystemAccount();

        var cart = CourseCart.Create(Guid.NewGuid(), buyer.Value, "buyer-ext-2", [
            new CartItemInput("product-gift", ProductItemType.Product, 1)
        ], "100OFF");

        // 100% discount on item, but shipping fee is 30
        cart.SetQuote([
            new CourseCartQuoteItem("product-gift", "هدیه", 50m, 50m, 0m, 0m, ProductItemType.Product, null, 1)
        ], shippingAmountInNoor: 30m);

        cart.Total.Should().Be(30m);

        var payment = CourseCartPayment.Apply(cart, new ProvisionedUserWallet(userAccount, wallet), systemAccount);

        wallet.Balance.Should().Be(70m);
        payment.PhysicalPurchases.Should().HaveCount(1);
        payment.PhysicalPurchases.Single().PaidAmountInNoor.Should().Be(0m);
        payment.PhysicalPurchases.Single().Quantity.Should().Be(1);

        // Debits exactly shipping amount
        payment.LedgerEntries.Should().HaveCount(2);
        payment.LedgerEntries.Single(e => e.Amount < 0).Amount.Should().Be(-30m);
        payment.LedgerEntries.Single(e => e.Amount > 0).Amount.Should().Be(30m);
    }

    [Fact]
    public void ZeroTotalCartWithZeroShipping_HasNoLedgerOrDebit()
    {
        var buyer = new UserId(Guid.NewGuid());
        var userAccount = Account.CreateUserAccount(buyer);
        var wallet = Wallet.Create(userAccount.Id, buyer);
        wallet.ApplyCredit(50m);

        var cart = CourseCart.Create(Guid.NewGuid(), buyer.Value, "buyer-ext-3", [
            new CartItemInput("product-free", ProductItemType.Product, 1)
        ], null);

        cart.SetQuote([
            new CourseCartQuoteItem("product-free", "رایگان", 0m, 0m, 0m, 0m, ProductItemType.Product, null, 1)
        ], shippingAmountInNoor: 0m);

        cart.Total.Should().Be(0m);

        var payment = CourseCartPayment.Apply(cart, new ProvisionedUserWallet(userAccount, wallet), system: null);

        wallet.Balance.Should().Be(50m);
        payment.LedgerEntries.Should().BeEmpty();
        payment.PhysicalPurchases.Should().HaveCount(1);
    }

    [Fact]
    public void ChangedQuantity_ChangesCartIdentityAndMatchesFails()
    {
        var buyer = Guid.NewGuid();
        var cartId = Guid.NewGuid();

        var cart = CourseCart.Create(cartId, buyer, "buyer-ext-4", [
            new CartItemInput("product-x", ProductItemType.Product, 2)
        ], null);

        // Matches with same quantity
        cart.Matches([new CartItemInput("product-x", ProductItemType.Product, 2)], null).Should().BeTrue();

        // Mismatches with changed quantity
        cart.Matches([new CartItemInput("product-x", ProductItemType.Product, 1)], null).Should().BeFalse();

        // Mismatches with changed coupon
        cart.Matches([new CartItemInput("product-x", ProductItemType.Product, 2)], "COUPON").Should().BeFalse();
    }

    [Fact]
    public void DuplicateProductInCart_MustBeAggregatedIntoSingleRow()
    {
        var validator = new PrepareCourseCartValidator();
        var duplicateCommand = new PrepareCourseCartCommand(Guid.NewGuid(), Items: [
            new CartItemInput("product-1", ProductItemType.Product, 1),
            new CartItemInput("product-1", ProductItemType.Product, 2)
        ]);

        var result = validator.Validate(duplicateCommand);
        result.IsValid.Should().BeFalse();

        var cart = CourseCart.Create(Guid.NewGuid(), Guid.NewGuid(), "buyer-1", [
            new CartItemInput("product-1", ProductItemType.Product, 1),
            new CartItemInput("product-2", ProductItemType.Product, 1)
        ], null);

        // Quote with duplicate items throws ArgumentException
        Action setDuplicateQuote = () => cart.SetQuote([
            new CourseCartQuoteItem("product-1", "کالا ۱", 10m, 0m, 0m, 10m, ProductItemType.Product, null, 1),
            new CourseCartQuoteItem("product-1", "کالا ۱ تکراری", 10m, 0m, 0m, 10m, ProductItemType.Product, null, 1)
        ]);

        setDuplicateQuote.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PhysicalProductPurchase_AllowsRepeatPurchaseBySameBuyer()
    {
        var buyer = new UserId(Guid.NewGuid());
        var p1 = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(),
            Guid.NewGuid(),
            buyer,
            "prod-repeat",
            "کتاب تکراری",
            1,
            100m,
            100m);

        var p2 = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(),
            Guid.NewGuid(),
            buyer,
            "prod-repeat",
            "کتاب تکراری",
            3,
            100m,
            300m);

        p1.BuyerId.Should().Be(buyer);
        p2.BuyerId.Should().Be(buyer);
        p1.ExternalProductId.Should().Be(p2.ExternalProductId);
        p1.Id.Should().NotBe(p2.Id);
    }

    [Fact]
    public void AllocationsPlusShippingMismatchWithTotal_ThrowsAndPreventsDebit()
    {
        var buyer = new UserId(Guid.NewGuid());
        var userAccount = Account.CreateUserAccount(buyer);
        var wallet = Wallet.Create(userAccount.Id, buyer);
        wallet.ApplyCredit(1000m);
        var systemAccount = Account.CreateSystemAccount();

        var cart = CourseCart.Create(Guid.NewGuid(), buyer.Value, "buyer-ext-err", [
            new CartItemInput("product-err", ProductItemType.Product, 1)
        ], null);

        // PriceInNoor arithmetic broken: LineTotal (100) - ItemDiscount(0) - CouponDiscount(0) != PriceInNoor(80)
        Action invalidPrice = () => cart.SetQuote([
            new CourseCartQuoteItem("product-err", "کالا", 100m, 0m, 0m, 80m, ProductItemType.Product, null, 1)
        ], shippingAmountInNoor: 10m);

        invalidPrice.Should().Throw<ArgumentException>();
        wallet.Balance.Should().Be(1000m); // untouched
    }
}
