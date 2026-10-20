using Transactions.Core.Domain;

namespace Transactions.Core.Import;

/// <summary>
/// Entry point for the import. The API calls it for manual triggers; an hourly scheduler
/// (Hangfire, Quartz, a hosted service, a cron'd console app) would resolve and call the same method.
/// </summary>
public interface ITransactionImportService
{
    /// <exception cref="ImportAlreadyRunningException">Another import is in progress.</exception>
    Task<ImportRun> RunAsync(string trigger = "manual", CancellationToken cancellationToken = default);
}

public sealed class ImportAlreadyRunningException()
    : InvalidOperationException("An import is already running. Try again when it has finished.");
