namespace Transactions.Core.Import;

public sealed class ImportOptions
{
    public const string SectionName = "Import";

    /// <summary>
    /// Missing transactions younger than this are revoked; older ones are finalized (immutable).
    /// </summary>
    public int ReconciliationWindowHours { get; set; } = 24;

    public TimeSpan ReconciliationWindow => TimeSpan.FromHours(ReconciliationWindowHours);
}
