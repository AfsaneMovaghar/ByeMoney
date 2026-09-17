using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Events;

public record TopUpConfirmed(
    TopUpRequestId TopUpRequestId,
    UserId UserId,
    decimal ConfirmedAmount,
    PendingItemType PendingItemType,
    string PendingItemExternalId,
    decimal PendingPriceSnapshot,
    decimal PendingRateSnapshot) : INotification;

