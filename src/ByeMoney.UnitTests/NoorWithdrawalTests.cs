using ByeMoney.Application.Modules.Wallet.Commands.CreateNoorWithdrawal;
using ByeMoney.Application.Modules.Wallet.Commands.MarkNoorWithdrawalPaid;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Withdrawals;
using FluentAssertions;

namespace ByeMoney.UnitTests;

public class NoorWithdrawalTests
{
    private readonly CreateNoorWithdrawalCommandValidator _validator = new();

    private static CreateNoorWithdrawalCommand Valid() => new(
        Guid.NewGuid(),
        "user-1",
        nameof(NoorWithdrawalKind.AdminDiscretionary),
        null,
        100_000m,
        "علی رضایی",
        "6037991234567890",
        null,
        "خروج به صلاحدید ادمین",
        null,
        null);

    [Fact]
    public void ValidRequest_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("1")]
    [InlineData("admindiscretionary")]
    [InlineData("Other")]
    public void Kind_AcceptsOnlyExactEnumName(string kind)
    {
        Error(Valid() with { Kind = kind }).Should().Be(NoorWithdrawalErrors.InvalidKind);
    }

    [Fact]
    public void DuplicateRefund_RequiresTopUp_AndDiscretionaryRejectsTopUp()
    {
        Error(Valid() with { Kind = nameof(NoorWithdrawalKind.DuplicatePaymentRefund) })
            .Should().Be(NoorWithdrawalErrors.TopUpRequired);
        Error(Valid() with { RelatedTopUpId = Guid.NewGuid() })
            .Should().Be(NoorWithdrawalErrors.TopUpNotAllowed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1.5)]
    public void AmountRial_MustBePositiveWholeNumber(decimal amount)
    {
        Error(Valid() with { AmountRial = amount }).Should().Be(NoorWithdrawalErrors.InvalidAmountRial);
    }

    [Fact]
    public void Beneficiary_NeedsCardOrIban_InValidFormat()
    {
        Error(Valid() with { BeneficiaryCardNumber = null })
            .Should().Be(NoorWithdrawalErrors.BeneficiaryAccountRequired);
        Error(Valid() with { BeneficiaryCardNumber = "60379912345678" })
            .Should().Be(NoorWithdrawalErrors.InvalidCardNumber);
        Error(Valid() with { BeneficiaryCardNumber = null, BeneficiaryIban = "IR12345678901234567890123A" })
            .Should().Be(NoorWithdrawalErrors.InvalidIban);
        _validator.Validate(Valid() with { BeneficiaryCardNumber = null, BeneficiaryIban = "IR123456789012345678901234" })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Description_IsRequired()
    {
        Error(Valid() with { Description = "  " }).Should().Be(NoorWithdrawalErrors.InvalidDescription);
    }

    [Fact]
    public void PaymentReferenceAndReceipt_MustComeTogether()
    {
        Error(Valid() with { PaymentReference = "BANK-1" }).Should().Be(NoorWithdrawalErrors.PaymentWithoutReference);
        Error(Valid() with { ReceiptId = "r.png" }).Should().Be(NoorWithdrawalErrors.PaymentWithoutReference);
        _validator.Validate(Valid() with { PaymentReference = "BANK-1", ReceiptId = "r.png" }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void MarkPaid_RequiresReceipt()
    {
        var result = new MarkNoorWithdrawalPaidCommandValidator()
            .Validate(new MarkNoorWithdrawalPaidCommand(Guid.NewGuid(), "BANK-1", ""));

        result.Errors.Single().ErrorMessage.Should().Be(NoorWithdrawalErrors.ReceiptRequired);
    }

    [Fact]
    public void Entity_AllowsOnlyOneTerminalTransition()
    {
        var withdrawal = NoorWithdrawal.Create(Guid.NewGuid(), new UserId(Guid.NewGuid()), NoorWithdrawalKind.AdminDiscretionary, null, 10m, 100_000m, "علی", "6037991234567890", null, "توضیح", new UserId(Guid.NewGuid()), Guid.NewGuid(), FinancialScope.TarheElahi);

        withdrawal.MarkReversed("برگشت", Guid.NewGuid(), new UserId(Guid.NewGuid()));

        withdrawal.Status.Should().Be(NoorWithdrawalStatus.Reversed);
        var markPaid = () => withdrawal.MarkPaid("BANK-1", "r.png", new UserId(Guid.NewGuid()));
        markPaid.Should().Throw<InvalidOperationException>();
    }

    private string Error(CreateNoorWithdrawalCommand command) =>
        _validator.Validate(command).Errors.First().ErrorMessage;
}
