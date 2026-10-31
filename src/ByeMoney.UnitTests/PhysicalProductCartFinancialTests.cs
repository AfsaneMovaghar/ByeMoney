using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Application.Modules.Purchases.Commands.PrepareCart;
using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.History.Services;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Domain.Common.Exceptions;
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
        var cart = Cart.Create(cartId, buyer.Value, "buyer-ext-1", [
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
            new CartQuoteItem("course-1", "Ø¯ÙˆØ±Ù‡ Ø¢Ù…ÙˆØ²Ø´ÛŒ", 100m, 10m, 10m, 80m, ProductItemType.Course, null, 1),
            new CartQuoteItem("product-1", "Ú©ØªØ§Ø¨ ÙÛŒØ²ÛŒÚ©ÛŒ", 50m, 20m, 10m, 70m, ProductItemType.Product, null, 2)
        ], shippingAmountInNoor: 25m);

        cart.OriginalTotal.Should().Be(200m);
        cart.Total.Should().Be(175m);
        cart.ShippingAmountInNoor.Should().Be(25m);
        cart.Items.Select(x => x.AllocatedAmountInNoor).Should().Equal(80m, 70m);
        (cart.Items.Sum(x => x.AllocatedAmountInNoor) + cart.ShippingAmountInNoor).Should().Be(cart.Total);

        var payment = CartPayment.Apply(cart, new ProvisionedUserWallet(userAccount, wallet), systemAccount);

        wallet.WithdrawableBalance.Should().Be(825m);
        cart.Status.Should().Be(CartStatus.Paid);

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
        physicalPurchase.ProductTitle.Should().Be("Ú©ØªØ§Ø¨ ÙÛŒØ²ÛŒÚ©ÛŒ");
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
        (cart.Items.Sum(x => x.AllocatedAmountInNoor) + cart.ShippingAmountInNoor)
            .Should().Be(-payment.LedgerEntries.Single(e => e.Amount < 0).Amount);
    }

    [Fact]
    public void FreeItemsWithPositiveShipping_IsNotFreeAndDebitsShippingAmount()
    {
        var buyer = new UserId(Guid.NewGuid());
        var userAccount = Account.CreateUserAccount(buyer);
        var wallet = Wallet.Create(userAccount.Id, buyer);
        wallet.ApplyCredit(100m);
        var systemAccount = Account.CreateSystemAccount();

        var cart = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer-ext-2", [
            new CartItemInput("product-gift", ProductItemType.Product, 1)
        ], "100OFF");

        // 100% discount on item, but shipping fee is 30
        cart.SetQuote([
            new CartQuoteItem("product-gift", "Ù‡Ø¯ÛŒÙ‡", 50m, 50m, 0m, 0m, ProductItemType.Product, null, 1)
        ], shippingAmountInNoor: 30m);

        cart.Total.Should().Be(30m);

        var payment = CartPayment.Apply(cart, new ProvisionedUserWallet(userAccount, wallet), systemAccount);

        wallet.WithdrawableBalance.Should().Be(70m);
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

        var cart = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer-ext-3", [
            new CartItemInput("product-free", ProductItemType.Product, 1)
        ], null);

        cart.SetQuote([
            new CartQuoteItem("product-free", "Ø±Ø§ÛŒÚ¯Ø§Ù†", 0m, 0m, 0m, 0m, ProductItemType.Product, null, 1)
        ], shippingAmountInNoor: 0m);

        cart.Total.Should().Be(0m);

        var payment = CartPayment.Apply(cart, new ProvisionedUserWallet(userAccount, wallet), system: null);

        wallet.WithdrawableBalance.Should().Be(50m);
        payment.LedgerEntries.Should().BeEmpty();
        payment.PhysicalPurchases.Should().HaveCount(1);
        payment.PhysicalPurchases.Single().LedgerTransactionId.Should().Be(cart.Id);
    }

    [Fact]
    public void PhysicalProductPurchase_MarkFree_OnPaidProduct_ThrowsDomainException()
    {
        var product = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(),
            Guid.NewGuid(),
            UserId.New(),
            "prod-1",
            "Ú©ØªØ§Ø¨",
            1,
            100m,
            100m);

        var act = () => product.MarkFree(Guid.NewGuid());

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void PhysicalProductPurchase_MarkFree_WithEmptyGuid_ThrowsDomainException()
    {
        var product = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(),
            Guid.NewGuid(),
            UserId.New(),
            "prod-free",
            "Ù‡Ø¯ÛŒÙ‡",
            1,
            0m,
            0m);

        var act = () => product.MarkFree(Guid.Empty);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ChangedQuantity_ChangesCartIdentityAndMatchesFails()
    {
        var buyer = Guid.NewGuid();
        var cartId = Guid.NewGuid();

        var cart = Cart.Create(cartId, buyer, "buyer-ext-4", [
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
        var validator = new PrepareCartValidator();
        var duplicateCommand = new PrepareCartCommand(Guid.NewGuid(), Items: [
            new CartItemInput("product-1", ProductItemType.Product, 1),
            new CartItemInput("product-1", ProductItemType.Product, 2)
        ]);

        var result = validator.Validate(duplicateCommand);
        result.IsValid.Should().BeFalse();

        var cart = Cart.Create(Guid.NewGuid(), Guid.NewGuid(), "buyer-1", [
            new CartItemInput("product-1", ProductItemType.Product, 1),
            new CartItemInput("product-2", ProductItemType.Product, 1)
        ], null);

        // Quote with duplicate items throws ArgumentException
        Action setDuplicateQuote = () => cart.SetQuote([
            new CartQuoteItem("product-1", "Ú©Ø§Ù„Ø§ Û±", 10m, 0m, 0m, 10m, ProductItemType.Product, null, 1),
            new CartQuoteItem("product-1", "Ú©Ø§Ù„Ø§ Û± ØªÚ©Ø±Ø§Ø±ÛŒ", 10m, 0m, 0m, 10m, ProductItemType.Product, null, 1)
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
            "Ú©ØªØ§Ø¨ ØªÚ©Ø±Ø§Ø±ÛŒ",
            1,
            100m,
            100m);

        var p2 = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(),
            Guid.NewGuid(),
            buyer,
            "prod-repeat",
            "Ú©ØªØ§Ø¨ ØªÚ©Ø±Ø§Ø±ÛŒ",
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

        var cart = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer-ext-err", [
            new CartItemInput("product-err", ProductItemType.Product, 1)
        ], null);

        // PriceInNoor arithmetic broken: LineTotal (100) - ItemDiscount(0) - CouponDiscount(0) != PriceInNoor(80)
        Action invalidPrice = () => cart.SetQuote([
            new CartQuoteItem("product-err", "Ú©Ø§Ù„Ø§", 100m, 0m, 0m, 80m, ProductItemType.Product, null, 1)
        ], shippingAmountInNoor: 10m);

        invalidPrice.Should().Throw<ArgumentException>();
        wallet.WithdrawableBalance.Should().Be(1000m); // untouched
    }

    [Fact]
    public void ShippingRuleLabelIsRecordedAndExposedOnCart()
    {
        var buyer = new UserId(Guid.NewGuid());
        var cart = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer-ext-label", [
            new CartItemInput("product-label", ProductItemType.Product, 1)
        ], null);

        cart.SetQuote([
            new CartQuoteItem("product-label", "Ú©ØªØ§Ø¨", 100m, 0m, 0m, 100m, ProductItemType.Product, null, 1)
        ], shippingAmountInNoor: 20m, shippingRuleLabel: "Ø§Ø±Ø³Ø§Ù„ Ø«Ø§Ø¨Øª Ø³Ø±Ø§Ø³Ø±ÛŒ");

        cart.ShippingAmountInNoor.Should().Be(20m);
        cart.ShippingRuleLabel.Should().Be("Ø§Ø±Ø³Ø§Ù„ Ø«Ø§Ø¨Øª Ø³Ø±Ø§Ø³Ø±ÛŒ");
    }

    [Fact]
    public void PhysicalProductPurchaseReceipt_ComputesCorrectBaseAmountAndDiscounts()
    {
        var buyer = new UserId(Guid.NewGuid());
        var cart = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer-ext-receipt", [
            new CartItemInput("product-book", ProductItemType.Product, 3)
        ], "OFFER");

        // 3 items * 50 = 150 base. Discounts: 30 item discount, 20 coupon discount => 100 paid
        cart.SetQuote([
            new CartQuoteItem("product-book", "Ú©ØªØ§Ø¨ ÙÛŒØ²ÛŒÚ©ÛŒ", 50m, 30m, 20m, 100m, ProductItemType.Product, null, 3)
        ], shippingAmountInNoor: 15m, shippingRuleLabel: "Ø§Ø±Ø³Ø§Ù„ ÙˆÛŒÚ˜Ù‡");

        var physicalId = PhysicalProductPurchaseId.New();
        var physical = PhysicalProductPurchase.Create(
            physicalId,
            cart.Id,
            buyer,
            "product-book",
            "Ú©ØªØ§Ø¨ ÙÛŒØ²ÛŒÚ©ÛŒ",
            3,
            50m,
            100m);
        physical.MarkDebited(cart.Id);

        var receiptData = new PurchaseReceiptData(null, cart, physical);
        var receipt = PurchaseHistoryMapping.ToReceipt(receiptData);

        receipt.PurchaseId.Should().Be(physicalId.Value);
        receipt.CartId.Should().Be(cart.Id);
        receipt.TransactionId.Should().Be(cart.Id);
        receipt.CourseExternalId.Should().Be("product-book");
        receipt.CourseTitle.Should().Be("Ú©ØªØ§Ø¨ ÙÛŒØ²ÛŒÚ©ÛŒ");
        receipt.BaseAmountInNoor.Should().Be(150m);
        receipt.DiscountAmountInNoor.Should().Be(50m);
        receipt.FinalAmountInNoor.Should().Be(100m);
        receipt.CouponCode.Should().Be("OFFER");
        receipt.FinancialStatus.Should().Be("Paid");
        receipt.DeliveryStatus.Should().Be("Pending");
        receipt.ItemType.Should().Be("Product");
        receipt.CartSummary.Should().NotBeNull();
        receipt.CartSummary!.OriginalTotalInNoor.Should().Be(150m);
        receipt.CartSummary.FinalAmountInNoor.Should().Be(cart.Total);
    }

    [Fact]
    public void PurchaseListItem_MapsPhysicalProductType()
    {
        var row = new PurchaseReadRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ú©Ø§Ù„Ø§ÛŒ Ø¢Ø²Ù…Ø§ÛŒØ´ÛŒ",
            200m,
            DateTime.UtcNow,
            CoursePurchaseStatus.Debited,
            "user-ext",
            "Ø¹Ù„ÛŒ",
            "Ù…Ø­Ù…Ø¯ÛŒ",
            ProductItemType.Product);

        var item = PurchaseHistoryMapping.ToListItem(row);

        item.ItemType.Should().Be("Product");
        item.FinancialStatus.Should().Be("Paid");
        item.DeliveryStatus.Should().Be("Pending");
        item.FinalAmountInNoor.Should().Be(200m);
    }

    [Theory]
    [InlineData("course", ProductItemType.Course)]
    [InlineData("Course", ProductItemType.Course)]
    [InlineData("course_chapter", ProductItemType.CourseChapter)]
    [InlineData("CourseChapter", ProductItemType.CourseChapter)]
    [InlineData("chapter", ProductItemType.CourseChapter)]
    [InlineData("product", ProductItemType.Product)]
    [InlineData("Product", ProductItemType.Product)]
    public void CartItemInput_DeserializesTypeFromString(string typeString, ProductItemType expected)
    {
        var json = $"{{\"externalId\":\"test-id\",\"type\":\"{typeString}\",\"quantity\":2}}";
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var item = System.Text.Json.JsonSerializer.Deserialize<CartItemInput>(json, options);

        item.Should().NotBeNull();
        item!.ExternalId.Should().Be("test-id");
        item.Type.Should().Be(expected);
        item.Quantity.Should().Be(2);
    }

    [Fact]
    public void PrepareCourseCartRequest_DeserializesFrontendPayloadSuccessfully()
    {
        var json = """
        {
            "cartId": "11111111-2222-3333-4444-555555555555",
            "items": [
                {
                    "externalId": "chap-123",
                    "type": "course_chapter",
                    "quantity": 1
                },
                {
                    "externalId": "prod-456",
                    "type": "product",
                    "quantity": 3
                }
            ],
            "externalCourseIds": ["chap-123"],
            "couponCode": "OFF10"
        }
        """;
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var request = System.Text.Json.JsonSerializer.Deserialize<ByeMoney.API.Contracts.CourseCarts.PrepareCourseCartRequest>(json, options);

        request.Should().NotBeNull();
        request!.CartId.Should().Be(Guid.Parse("11111111-2222-3333-4444-555555555555"));
        request.Items.Should().HaveCount(2);
        request.Items![0].ExternalId.Should().Be("chap-123");
        request.Items[0].Type.Should().Be(ProductItemType.CourseChapter);
        request.Items[0].Quantity.Should().Be(1);
        request.Items[1].ExternalId.Should().Be("prod-456");
        request.Items[1].Type.Should().Be(ProductItemType.Product);
        request.Items[1].Quantity.Should().Be(3);
        request.ExternalCourseIds.Should().ContainSingle().Which.Should().Be("chap-123");
        request.CouponCode.Should().Be("OFF10");
    }
}

