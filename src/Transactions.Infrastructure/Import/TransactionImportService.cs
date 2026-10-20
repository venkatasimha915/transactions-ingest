using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Transactions.Core.Domain;
using Transactions.Core.Import;
using Transactions.Core.Sources;
using Transactions.Infrastructure.Persistence;

namespace Transactions.Infrastructure.Import;

/// <summary>
/// Orchestrates one import: fetch each source, reconcile it (pure logic in <see cref="TransactionReconciler"/>),
/// and persist the result. Each source is saved in its own database transaction (one SaveChanges), so a failure
/// in one source neither rolls back nor blocks the others, and a source that cannot be fetched is skipped
/// entirely — an outage must never look like "everything was removed" and trigger mass revocations.
/// </summary>
public sealed class TransactionImportService(
    AppDbContext db,
    IEnumerable<ITransactionSource> sources,
    ImportRunLock runLock,
    TimeProvider clock,
    IOptions<ImportOptions> options,
    ILogger<TransactionImportService> logger) : ITransactionImportService
{
    public async Task<ImportRun> RunAsync(string trigger = "manual", CancellationToken cancellationToken = default)
    {
        using var lease = await runLock.TryAcquireAsync(cancellationToken)
                          ?? throw new ImportAlreadyRunningException();
        return await RunCoreAsync(trigger, cancellationToken);
    }

    private async Task<ImportRun> RunCoreAsync(string trigger, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var run = new ImportRun
        {
            Id = Guid.NewGuid(),
            Trigger = trigger,
            StartedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        db.ImportRuns.Add(run);
        await db.SaveChangesAsync(ct); // audit rows reference the run

        using var _ = logger.BeginScope(new Dictionary<string, object> { ["ImportRunId"] = run.Id });
        logger.LogInformation("Import {ImportRunId} started ({Trigger})", run.Id, trigger);

        var failed = new List<string>();
        foreach (var source in sources)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var result = await ImportSourceAsync(source, run.Id, ct);
                result.AddTo(run);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Nothing for this source was saved (its SaveChanges never committed), so no revocations happened.
                logger.LogError(ex, "Import of source {Source} failed; source skipped, no revocations applied", source.Name);
                db.ChangeTracker.Clear();
                failed.Add(source.Name);
            }
        }

        run.FailedSources = failed.Count > 0 ? string.Join(", ", failed) : null;
        run.CompletedAtUtc = clock.GetUtcNow().UtcDateTime;
        db.ImportRuns.Update(run);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Import {ImportRunId} finished in {ElapsedMs} ms: inserted={Inserted} updated={Updated} revoked={Revoked} " +
            "reactivated={Reactivated} finalized={Finalized} unchanged={Unchanged} rejected={Rejected} failedSources={FailedSources}",
            run.Id, stopwatch.ElapsedMilliseconds, run.Inserted, run.Updated, run.Revoked,
            run.Reactivated, run.Finalized, run.Unchanged, run.Rejected, run.FailedSources ?? "none");

        return run;
    }

    private async Task<ReconciliationResult> ImportSourceAsync(ITransactionSource source, Guid runId, CancellationToken ct)
    {
        var raw = await source.FetchSnapshotAsync(ct);
        var normalized = SnapshotNormalizer.Normalize(source.Name, raw);
        foreach (var rejection in normalized.Rejections)
            logger.LogWarning("Rejected snapshot item from {Source}: {Reason}", source.Name, rejection);
        foreach (var warning in normalized.Warnings)
            logger.LogWarning("{Warning}", warning);

        var incomingIds = normalized.Accepted.Select(s => s.TransactionId).ToList();

        // Only rows that can still change (not finalized) plus any row the snapshot mentions.
        // Finalized history is never loaded wholesale, so the query stays bounded as data grows.
        var existing = await db.Transactions
            .Where(t => t.SourceSystem == source.Name &&
                        (t.Status != TransactionStatus.Finalized || incomingIds.Contains(t.TransactionId)))
            .ToListAsync(ct);

        var result = TransactionReconciler.Reconcile(
            source.Name, existing, normalized.Accepted, runId,
            clock.GetUtcNow().UtcDateTime, options.Value.ReconciliationWindow);
        result.Rejected = normalized.Rejections.Count;

        foreach (var warning in result.Warnings)
            logger.LogWarning("{Warning}", warning);

        if (result.NewTransactions.Count > 0)
            db.Transactions.AddRange(result.NewTransactions);

        // One SaveChanges = one database transaction for the whole source.
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Source {Source}: received={Received} inserted={Inserted} updated={Updated} revoked={Revoked} " +
            "reactivated={Reactivated} finalized={Finalized} unchanged={Unchanged} rejected={Rejected}",
            source.Name, raw.Count, result.Inserted, result.Updated, result.Revoked,
            result.Reactivated, result.Finalized, result.Unchanged, result.Rejected);

        return result;
    }
}
