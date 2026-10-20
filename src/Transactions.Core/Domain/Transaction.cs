namespace Transactions.Core.Domain;

/// <summary>
/// A card transaction as known by this platform.
/// The logical key is (<see cref="SourceSystem"/>, <see cref="TransactionId"/>);
/// <see cref="Id"/> is a surrogate key used for relationships and URLs.
/// </summary>
public class Transaction
{
    public const int MaxTextLength = 20;

    public int Id { get; set; }

    public string SourceSystem { get; set; } = string.Empty;
    public int TransactionId { get; set; }

    /// <summary>Masked card number (e.g. "***1111"). Empty for non-card payments.</summary>
    public string CardNumber { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    /// <summary>When the transaction happened in the source system (UTC).</summary>
    public DateTime TransactionTime { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Active;

    /// <summary>Starts at 1 and increments on every change (data or status).</summary>
    public int Revision { get; set; } = 1;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public List<TransactionAudit> AuditEntries { get; set; } = [];
}
