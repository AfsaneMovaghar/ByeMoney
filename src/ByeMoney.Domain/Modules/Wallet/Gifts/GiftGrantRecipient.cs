using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Domain.Modules.Wallet.Gifts;

public class GiftGrantRecipient : BaseEntity<Guid>
{
    public Guid BatchId { get; private set; }
    public UserId UserId { get; private set; }
    public string ExternalUserId { get; private set; } = string.Empty;
    public decimal AmountNoor { get; private set; }
    public Guid TransactionId { get; private set; }
    public GiftRecipientStatus Status { get; private set; } = GiftRecipientStatus.Granted;
    public decimal NewGiftBalance { get; private set; }
    public DateTime GrantedAtUtc { get; private set; }

    private GiftGrantRecipient() { }

    public static GiftGrantRecipient Create(
        Guid batchId,
        UserId userId,
        string externalUserId,
        decimal amountNoor,
        Guid transactionId,
        decimal newGiftBalance,
        DateTime grantedAtUtc,
        GiftRecipientStatus status = GiftRecipientStatus.Granted)
    {
        return new GiftGrantRecipient
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            UserId = userId,
            ExternalUserId = externalUserId,
            AmountNoor = amountNoor,
            TransactionId = transactionId,
            NewGiftBalance = newGiftBalance,
            GrantedAtUtc = grantedAtUtc,
            Status = status
        };
    }
}

