namespace Transactions.Core.Domain;

public enum TransactionStatus
{
    /// <summary>Present in the latest snapshot and still inside the reconciliation window.</summary>
    Active,
    /// <summary>Disappeared from the source snapshot while inside the reconciliation window.</summary>
    Revoked,
    /// <summary>Older than the reconciliation window; immutable from now on.</summary>
    Finalized
}
