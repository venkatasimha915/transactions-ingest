namespace Transactions.Core.Sources;

/// <summary>
/// An upstream source system. Each call returns the full current snapshot the source publishes
/// (at minimum, everything inside the reconciliation window).
/// Real HTTP clients would implement this; the exercise uses JSON-file mocks.
/// </summary>
public interface ITransactionSource
{
    string Name { get; }

    Task<IReadOnlyList<SourceTransaction>> FetchSnapshotAsync(CancellationToken cancellationToken);
}
