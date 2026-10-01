namespace ByeMoney.API.Contracts.TopUp;

using Microsoft.AspNetCore.Http;

public record AdminAssistedTopUpApiRequest(
    string BeneficiaryExternalUserId,
    decimal AmountToman,
    IFormFile Receipt,
    string? ExternalTransactionId = null);

