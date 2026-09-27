namespace ByeMoney.Application.Common.Interfaces;

public interface IReceiptStorage
{
    Task<string> SaveAsync(Stream content, string fileName, string idempotencyKey, CancellationToken ct = default);
    Task<bool> ExistsAsync(string receiptId, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string receiptId, CancellationToken ct = default);
    Task DeleteAsync(string receiptId, CancellationToken ct = default);
}