using Transactions.Core.Domain;

namespace Transactions.Core.Import;

/// <summary>Outcome of reconciling one source snapshot. Counters are merged into the <see cref="ImportRun"/>.</summary>
public sealed class ReconciliationResult
{
    public List<Transaction> NewTransactions { get; } = [];
    public List<string> Warnings { get; } = [];

    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Revoked { get; set; }
    public int Reactivated { get; set; }
    public int Finalized { get; set; }
    public int Unchanged { get; set; }
    public int Rejected { get; set; }

    public bool HasChanges => Inserted + Updated + Revoked + Reactivated + Finalized > 0;

    public void AddTo(ImportRun run)
    {
        run.Inserted += Inserted;
        run.Updated += Updated;
        run.Revoked += Revoked;
        run.Reactivated += Reactivated;
        run.Finalized += Finalized;
        run.Unchanged += Unchanged;
        run.Rejected += Rejected;
    }
}
