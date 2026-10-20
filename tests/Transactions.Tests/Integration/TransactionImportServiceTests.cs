using Transactions.Core.Domain;
using Transactions.Tests.Support;
using static Transactions.Tests.Support.TestData;

namespace Transactions.Tests.Integration;

public class TransactionImportServiceTests
{
    [Fact]
    public async Task Inserts_new_transactions_into_sqlite()
    {
        await using var h = await ImportHarness.CreateAsync();
        h.Online.Snapshot = [Tx(1), Tx(2)];
        h.Uptown.Snapshot = [Tx(1, source: "Store Uptown", location: "UPTOWN")];

        var run = await h.RunAsync();

        Assert.Equal(3, run.Inserted);
        Assert.True(run.Succeeded);
        Assert.Equal(3, (await h.AllTransactionsAsync()).Count);
    }

    [Fact]
    public async Task Same_transaction_id_in_different_sources_is_two_records()
    {
        await using var h = await ImportHarness.CreateAsync();
        h.Uptown.Snapshot = [Tx(100001, source: "Store Uptown", location: "UPTOWN")];
        h.Downtown.Snapshot = [Tx(100001, source: "Store Downtown", location: "DOWNTOWN")];
        h.Online.Snapshot = [Tx(100001)];

        await h.RunAsync();

        Assert.Equal(3, (await h.AllTransactionsAsync()).Count(t => t.TransactionId == 100001));
    }

    [Fact]
    public async Task Running_twice_with_identical_data_is_idempotent()
    {
        await using var h = await ImportHarness.CreateAsync();
        h.Online.Snapshot = [Tx(1), Tx(2, amount: 99.99m), Tx(3, card: null)];

        await h.RunAsync();
        var before = await h.AllTransactionsAsync();
        var auditsBefore = await h.AuditCountAsync();

        h.Clock.Advance(TimeSpan.FromMinutes(10));
        var second = await h.RunAsync();
        var after = await h.AllTransactionsAsync();

        Assert.Equal(0, second.Inserted + second.Updated + second.Revoked + second.Reactivated + second.Finalized);
        Assert.Equal(3, second.Unchanged);
        Assert.Equal(auditsBefore, await h.AuditCountAsync());
        Assert.Equal(
            before.Select(t => (t.Id, t.Amount, t.Status, t.Revision, t.UpdatedAtUtc)),
            after.Select(t => (t.Id, t.Amount, t.Status, t.Revision, t.UpdatedAtUtc)));
    }

    [Fact]
    public async Task Changed_amount_is_updated_and_audited_with_old_and_new_values()
    {
        await using var h = await ImportHarness.CreateAsync();
        h.Online.Snapshot = [Tx(1, amount: 24.99m)];
        await h.RunAsync();

        h.Online.Snapshot = [Tx(1, amount: 19.99m)];
        var run = await h.RunAsync();

        var t = await h.GetAsync("Online", 1);
        Assert.Equal(1, run.Updated);
        Assert.Equal(19.99m, t.Amount);
        Assert.Equal(2, t.Revision);
        var audit = Assert.Single(t.AuditEntries, a => a.Action == AuditAction.Updated);
        Assert.Equal(("Amount", "24.99", "19.99"), (audit.FieldName, audit.OldValue, audit.NewValue));
        Assert.Equal(run.Id, audit.ImportRunId);
    }

    [Fact]
    public async Task Removed_transaction_within_24h_is_revoked_and_audited()
    {
        await using var h = await ImportHarness.CreateAsync();
        h.Online.Snapshot = [Tx(1), Tx(2)];
        await h.RunAsync();

        h.Online.Snapshot = [Tx(1)];
        var run = await h.RunAsync();

        var t = await h.GetAsync("Online", 2);
        Assert.Equal(1, run.Revoked);
        Assert.Equal(TransactionStatus.Revoked, t.Status);
        Assert.Contains(t.AuditEntries, a => a.Action == AuditAction.Revoked);

        // Revocation itself is idempotent too.
        var again = await h.RunAsync();
        Assert.Equal(0, again.Revoked);
    }

    [Fact]
    public async Task Transactions_age_into_finalized_and_then_ignore_source_changes()
    {
        await using var h = await ImportHarness.CreateAsync();
        h.Online.Snapshot = [Tx(1, amount: 10m, hoursAgo: 1)];
        await h.RunAsync();

        h.Clock.Advance(TimeSpan.FromHours(24));          // now 25h old
        h.Online.Snapshot = [];                            // and gone from the source
        var run = await h.RunAsync();

        Assert.Equal(1, run.Finalized);
        Assert.Equal(0, run.Revoked);
        Assert.Equal(TransactionStatus.Finalized, (await h.GetAsync("Online", 1)).Status);

        h.Online.Snapshot = [Tx(1, amount: 500m, hoursAgo: 1) with { TransactionTime = Now.AddHours(-1) }];
        await h.RunAsync();
        Assert.Equal(10m, (await h.GetAsync("Online", 1)).Amount);
    }

    [Fact]
    public async Task Failing_source_is_skipped_without_revocations_and_other_sources_still_import()
    {
        await using var h = await ImportHarness.CreateAsync();
        h.Online.Snapshot = [Tx(1), Tx(2)];
        await h.RunAsync();

        h.Online.Fail = true;
        h.Uptown.Snapshot = [Tx(7, source: "Store Uptown", location: "UPTOWN")];
        var run = await h.RunAsync();

        Assert.False(run.Succeeded);
        Assert.Equal("Online", run.FailedSources);
        Assert.Equal(1, run.Inserted);
        Assert.All((await h.AllTransactionsAsync()).Where(t => t.SourceSystem == "Online"),
            t => Assert.Equal(TransactionStatus.Active, t.Status));
    }

    [Fact]
    public async Task Revoked_transaction_that_reappears_is_reactivated()
    {
        await using var h = await ImportHarness.CreateAsync();
        h.Online.Snapshot = [Tx(1)];
        await h.RunAsync();
        h.Online.Snapshot = [];
        await h.RunAsync();

        h.Online.Snapshot = [Tx(1)];
        var run = await h.RunAsync();

        Assert.Equal(1, run.Reactivated);
        var t = await h.GetAsync("Online", 1);
        Assert.Equal(TransactionStatus.Active, t.Status);
        Assert.Equal(3, t.Revision);
        Assert.Equal(
            new[] { AuditAction.Created, AuditAction.Revoked, AuditAction.Reactivated },
            t.AuditEntries.OrderBy(a => a.Id).Select(a => a.Action));
    }

    [Fact]
    public async Task Amount_and_time_round_trip_exactly_through_sqlite()
    {
        await using var h = await ImportHarness.CreateAsync();
        var time = new DateTime(2026, 10, 20, 11, 15, 30, 123, DateTimeKind.Utc);
        h.Online.Snapshot = [Tx(1, amount: 1234567.89m) with { TransactionTime = time }];

        await h.RunAsync();

        var t = await h.GetAsync("Online", 1);
        Assert.Equal(1234567.89m, t.Amount);
        Assert.Equal(time, t.TransactionTime);
        Assert.Equal(DateTimeKind.Utc, t.TransactionTime.Kind);
    }
}

public class ImportRunLockTests
{
    [Fact]
    public async Task Second_concurrent_import_is_rejected()
    {
        await using var h = await Support.ImportHarness.CreateAsync();
        using var held = await h.Lock.TryAcquireAsync(CancellationToken.None);

        await Assert.ThrowsAsync<Core.Import.ImportAlreadyRunningException>(h.RunAsync);
    }
}
