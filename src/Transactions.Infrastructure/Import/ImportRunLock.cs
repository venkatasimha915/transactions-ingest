namespace Transactions.Infrastructure.Import;

/// <summary>
/// Prevents overlapping imports inside one process (e.g. a manual click while the hourly job runs).
/// Registered as a singleton so every scope shares it; tests get their own instance.
/// A multi-instance deployment would replace this with a distributed lock (DB row / Redis / blob lease).
/// </summary>
public sealed class ImportRunLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IDisposable?> TryAcquireAsync(CancellationToken ct) =>
        await _semaphore.WaitAsync(TimeSpan.Zero, ct) ? new Releaser(_semaphore) : null;

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) semaphore.Release();
        }
    }
}
