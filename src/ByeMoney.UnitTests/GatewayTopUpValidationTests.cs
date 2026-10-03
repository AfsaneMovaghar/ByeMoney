using ByeMoney.Application.Modules.Wallet.Commands.CancelGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;
using FluentAssertions;

namespace ByeMoney.UnitTests;

public class GatewayTopUpValidationTests
{
    [Fact]
    public void ConfirmGatewayTopUpCommandValidator_ShouldPass_ForValidInput()
    {
        var validator = new ConfirmGatewayTopUpCommandValidator();
        var command = new ConfirmGatewayTopUpCommand(
            "REF-12345",
            "SEP",
            "TX-999",
            "RRN-888",
            100_000m,
            100_000m);

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "SEP", "TX", "RRN", 100, 100)]
    [InlineData("REF", "", "TX", "RRN", 100, 100)]
    [InlineData("REF", "SEP", "", "RRN", 100, 100)]
    [InlineData("REF", "SEP", "TX", "", 100, 100)]
    [InlineData("REF", "SEP", "TX", "RRN", 0, 100)]
    [InlineData("REF", "SEP", "TX", "RRN", 100, -1)]
    public void ConfirmGatewayTopUpCommandValidator_ShouldFail_ForInvalidInput(
        string refCode, string gateway, string txId, string rrn, decimal original, decimal affective)
    {
        var validator = new ConfirmGatewayTopUpCommandValidator();
        var command = new ConfirmGatewayTopUpCommand(refCode, gateway, txId, rrn, original, affective);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CancelGatewayTopUpCommandValidator_ShouldPass_ForValidInput()
    {
        var validator = new CancelGatewayTopUpCommandValidator();
        var command = new CancelGatewayTopUpCommand("REF-12345", "SEP");

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "SEP")]
    [InlineData("REF", "")]
    public void CancelGatewayTopUpCommandValidator_ShouldFail_ForInvalidInput(string refCode, string gateway)
    {
        var validator = new CancelGatewayTopUpCommandValidator();
        var command = new CancelGatewayTopUpCommand(refCode, gateway);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }
}

