namespace Transactions.Core.Domain;

public enum AuditAction
{
    Created,
    Updated,
    Revoked,
    Reactivated,
    Finalized
}
