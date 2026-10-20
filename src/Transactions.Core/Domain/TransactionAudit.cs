namespace Transactions.Core.Domain;

/// <summary>
/// One audit event. Field-level updates produce one row per changed field
/// (<see cref="FieldName"/>, <see cref="OldValue"/>, <see cref="NewValue"/>).
/// </summary>
public class TransactionAudit
{
    public long Id { get; set; }

    public int TransactionRecordId { get; set; }
    public Transaction Transaction { get; set; } = null!;

    public Guid ImportRunId { get; set; }

    public AuditAction Action { get; set; }
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    public DateTime ChangedAtUtc { get; set; }
}
