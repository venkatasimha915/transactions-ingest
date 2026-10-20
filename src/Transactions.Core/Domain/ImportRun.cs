namespace Transactions.Core.Domain;

/// <summary>One execution of the import (manual or scheduled). Groups the audit rows it produced.</summary>
public class ImportRun
{
    public Guid Id { get; set; }
    public string Trigger { get; set; } = "manual";
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Revoked { get; set; }
    public int Reactivated { get; set; }
    public int Finalized { get; set; }
    public int Unchanged { get; set; }
    public int Rejected { get; set; }

    /// <summary>Comma separated names of sources that failed; null when all succeeded.</summary>
    public string? FailedSources { get; set; }

    public bool Succeeded => CompletedAtUtc is not null && FailedSources is null;
}
