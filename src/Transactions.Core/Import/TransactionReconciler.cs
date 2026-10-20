using Transactions.Core.Domain;
using Transactions.Core.Sources;

namespace Transactions.Core.Import;

/// <summary>
/// Pure reconciliation of one source snapshot against the stored transactions of that source.
/// No I/O: it mutates the supplied (tracked) entities, appends audit entries and returns new entities,
/// so the persistence layer only has to load, call this, and save.
///
/// Rules (window = 24h by default, measured from TransactionTime):
///  1. Stored Active rows older than the window are Finalized (immutable from then on).
///  2. Snapshot item not stored            -> insert (Finalized straight away if it arrives already outside the window).
///  3. Stored row is Finalized             -> ignore source changes (logged as a warning if it differs).
///  4. Stored row differs from item        -> update fields, one Updated audit per changed field.
///  5. Stored row is Revoked and reappears -> Reactivated (plus any field updates).
///  6. Stored Active row missing from the snapshot, inside the window -> Revoked.
///  7. Nothing differs                     -> no writes at all (idempotency).
/// </summary>
public static class TransactionReconciler
{
    public static ReconciliationResult Reconcile(
        string sourceName,
        IReadOnlyCollection<Transaction> existing,
        IReadOnlyList<SourceTransaction> snapshot,
        Guid importRunId,
        DateTime nowUtc,
        TimeSpan window)
    {
        var result = new ReconciliationResult();
        var byId = existing.ToDictionary(t => t.TransactionId);

        bool OutsideWindow(DateTime transactionTime) => nowUtc - transactionTime > window;

        // 1. Finalize everything that has aged out (Active only; a Revoked row simply stays Revoked).
        foreach (var t in existing.Where(t => t.Status == TransactionStatus.Active && OutsideWindow(t.TransactionTime)))
        {
            ChangeStatus(t, TransactionStatus.Finalized, AuditAction.Finalized, importRunId, nowUtc);
            result.Finalized++;
        }

        // 2-5. Inserts and updates.
        foreach (var item in snapshot)
        {
            if (!byId.TryGetValue(item.TransactionId, out var current))
            {
                var created = Create(sourceName, item, importRunId, nowUtc,
                    OutsideWindow(item.TransactionTime) ? TransactionStatus.Finalized : TransactionStatus.Active);
                result.NewTransactions.Add(created);
                result.Inserted++;
                continue;
            }

            var changes = TransactionDiff.Compare(current, item);

            if (current.Status == TransactionStatus.Finalized || OutsideWindow(current.TransactionTime))
            {
                if (changes.Count > 0 || current.Status == TransactionStatus.Revoked)
                    result.Warnings.Add($"{sourceName}/{item.TransactionId}: source changed a transaction outside the " +
                                        $"reconciliation window ({current.Status}); change ignored");
                result.Unchanged++;
                continue;
            }

            var reactivate = current.Status == TransactionStatus.Revoked;
            if (changes.Count == 0 && !reactivate)
            {
                result.Unchanged++;
                continue;
            }

            foreach (var change in changes)
                current.AuditEntries.Add(NewAudit(importRunId, nowUtc, AuditAction.Updated, change.Field, change.OldValue, change.NewValue));
            TransactionDiff.Apply(item, current);

            if (reactivate)
            {
                current.Status = TransactionStatus.Active;
                current.AuditEntries.Add(NewAudit(importRunId, nowUtc, AuditAction.Reactivated,
                    nameof(Transaction.Status), nameof(TransactionStatus.Revoked), nameof(TransactionStatus.Active)));
                result.Reactivated++;
            }
            if (changes.Count > 0)
                result.Updated++;

            current.Revision++;
            current.UpdatedAtUtc = nowUtc;
        }

        // 6. Revocations.
        var seen = snapshot.Select(s => s.TransactionId).ToHashSet();
        foreach (var t in existing.Where(t => t.Status == TransactionStatus.Active
                                           && !seen.Contains(t.TransactionId)
                                           && !OutsideWindow(t.TransactionTime)))
        {
            ChangeStatus(t, TransactionStatus.Revoked, AuditAction.Revoked, importRunId, nowUtc);
            result.Revoked++;
        }

        return result;
    }

    private static Transaction Create(string sourceName, SourceTransaction item, Guid runId, DateTime now, TransactionStatus status)
    {
        var t = new Transaction
        {
            SourceSystem = sourceName,
            TransactionId = item.TransactionId,
            Status = status,
            Revision = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        TransactionDiff.Apply(item, t);
        t.AuditEntries.Add(NewAudit(runId, now, AuditAction.Created, null, null, null));
        if (status == TransactionStatus.Finalized)
            t.AuditEntries.Add(NewAudit(runId, now, AuditAction.Finalized,
                nameof(Transaction.Status), null, nameof(TransactionStatus.Finalized)));
        return t;
    }

    private static void ChangeStatus(Transaction t, TransactionStatus newStatus, AuditAction action, Guid runId, DateTime now)
    {
        var old = t.Status;
        t.Status = newStatus;
        t.Revision++;
        t.UpdatedAtUtc = now;
        t.AuditEntries.Add(NewAudit(runId, now, action, nameof(Transaction.Status), old.ToString(), newStatus.ToString()));
    }

    private static TransactionAudit NewAudit(Guid runId, DateTime now, AuditAction action, string? field, string? oldValue, string? newValue) =>
        new()
        {
            ImportRunId = runId,
            Action = action,
            FieldName = field,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedAtUtc = now
        };
}
