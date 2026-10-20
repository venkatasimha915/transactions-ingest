using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Transactions.Core.Domain;
using Transactions.Core.Import;
using Transactions.Infrastructure.Import;
using Transactions.Infrastructure.Persistence;

namespace Transactions.Tests.Support;

/// <summary>
/// Real SQLite (in-memory, connection kept open) so the unique index, value converters and SQL translation
/// are exercised exactly as in production. Every run uses a fresh DbContext, like separate HTTP requests / job runs.
/// </summary>
internal sealed class ImportHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public ImportRunLock Lock { get; } = new();
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(TestData.Now));
    public FakeSource Uptown { get; } = new("Store Uptown");
    public FakeSource Downtown { get; } = new("Store Downtown");
    public FakeSource Online { get; } = new("Online");

    private ImportHarness() { }

    public static async Task<ImportHarness> CreateAsync()
    {
        var h = new ImportHarness();
        await h._connection.OpenAsync();
        await using var db = h.NewContext();
        await db.Database.EnsureCreatedAsync();
        return h;
    }

    public AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public async Task<ImportRun> RunAsync()
    {
        await using var db = NewContext();
        var service = new TransactionImportService(
            db, [Uptown, Downtown, Online], Lock, Clock,
            Options.Create(new ImportOptions { ReconciliationWindowHours = 24 }),
            NullLogger<TransactionImportService>.Instance);
        return await service.RunAsync("test");
    }

    public async Task<List<Transaction>> AllTransactionsAsync()
    {
        await using var db = NewContext();
        return await db.Transactions.AsNoTracking().OrderBy(t => t.SourceSystem).ThenBy(t => t.TransactionId).ToListAsync();
    }

    public async Task<Transaction> GetAsync(string source, int id)
    {
        await using var db = NewContext();
        return await db.Transactions.AsNoTracking().Include(t => t.AuditEntries)
            .SingleAsync(t => t.SourceSystem == source && t.TransactionId == id);
    }

    public async Task<int> AuditCountAsync()
    {
        await using var db = NewContext();
        return await db.TransactionAudits.CountAsync();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
