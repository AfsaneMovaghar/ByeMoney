namespace ByeMoney.API.Contracts.TopUp;

using Microsoft.AspNetCore.Http;

public record AdminAssistedTopUpApiRequest(
    string BeneficiaryExternalUserId,
    decimal AmountRial,
    IFormFile Receipt,
    string? ExternalTransactionId = null);

