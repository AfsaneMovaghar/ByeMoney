using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Domain.Modules.Wallet.Gifts;

public class GiftGrantBatch : BaseEntity<Guid>
{
    private readonly List<GiftGrantRecipient> _recipients = [];

    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestPayloadHash { get; private set; } = string.Empty;
    public GiftGrantSource Source { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public decimal AmountNoorPerUser { get; private set; }
    public int TotalRecipients { get; private set; }
    public decimal TotalAmountNoor { get; private set; }
    public UserId? AdminUserId { get; private set; }

    public IReadOnlyCollection<GiftGrantRecipient> Recipients => _recipients.AsReadOnly();

    private GiftGrantBatch() { }

    public static GiftGrantBatch Create(
        string idempotencyKey,
        string requestPayloadHash,
        GiftGrantSource source,
        string reason,
        decimal amountNoorPerUser,
        UserId? adminUserId,
        DateTime? createdAtUtc = null)
    {
        return new GiftGrantBatch
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = idempotencyKey,
            RequestPayloadHash = requestPayloadHash,
            Source = source,
            Reason = reason,
            AmountNoorPerUser = amountNoorPerUser,
            AdminUserId = adminUserId
        };
    }

    public void AddRecipient(GiftGrantRecipient recipient)
    {
        _recipients.Add(recipient);
        TotalRecipients = _recipients.Count;
        TotalAmountNoor = TotalRecipients * AmountNoorPerUser;
    }
}
