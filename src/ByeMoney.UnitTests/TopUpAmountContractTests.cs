using System.Text.Json;
using ByeMoney.API.Contracts.TopUp;
using ByeMoney.Application.Modules.Wallet.Queries.GetTopUpByClientReferenceCode;
using FluentAssertions;

namespace ByeMoney.UnitTests;

public class TopUpAmountContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CreateRequest_PreservesAmountJsonName()
    {
        var request = JsonSerializer.Deserialize<CreateTopUpApiRequest>("{\"amount\":25}", JsonOptions)!;

        request.AmountNoor.Should().Be(25m);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(request, JsonOptions));
        json.RootElement.GetProperty("amount").GetDecimal().Should().Be(25m);
        json.RootElement.TryGetProperty("amountNoor", out _).Should().BeFalse();
    }

    [Fact]
    public void ConfirmRequest_PreservesConfirmedAmountJsonName()
    {
        var request = JsonSerializer.Deserialize<AdminConfirmRequest>(
            "{\"externalTransactionId\":\"bank-ref\",\"confirmedAmount\":25}", JsonOptions)!;

        request.ConfirmedAmountNoor.Should().Be(25m);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(request, JsonOptions));
        json.RootElement.GetProperty("confirmedAmount").GetDecimal().Should().Be(25m);
        json.RootElement.TryGetProperty("confirmedAmountNoor", out _).Should().BeFalse();
    }

    [Fact]
    public void DetailsResponse_PreservesAmountJsonName()
    {
        var response = new TopUpRequestDto(Guid.NewGuid(), Guid.NewGuid(), 25m, "Gateway", "TR-TEST",
            "Pending", null, null, DateTime.UtcNow, null, null, null, null, 250000m, 10000m, false);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, JsonOptions));
        json.RootElement.GetProperty("amount").GetDecimal().Should().Be(25m);
        json.RootElement.TryGetProperty("amountNoor", out _).Should().BeFalse();
    }
}
