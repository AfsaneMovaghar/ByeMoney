using System;
using System.Collections.Generic;
using System.Text;

namespace ByeMoney.Domain._Modules.Wallet.Ledgers;

public enum LedgerReferenceType
{
    TopUp = 1,
    Purchase = 2,
    Refund = 3,
    AdminGrant = 4
}

