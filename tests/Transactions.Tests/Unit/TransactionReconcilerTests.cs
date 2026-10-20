using Transactions.Core.Domain;
using Transactions.Core.Import;
using Transactions.Core.Sources;
using static Transactions.Tests.Support.TestData;

namespace Transactions.Tests.Unit;

/// <summary>Pure reconciliation rules, no database involved.</summary>
public class TransactionReconcilerTests
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);
    private static readonly Guid RunId = Guid.NewGuid();

    private static ReconciliationResult Reconcile(List<Transaction> existing, params SourceTransaction[] snapshot) =>
        TransactionReconciler.Reconcile("Online", existing, snapshot, RunId, Now, Window);

    private static Transaction Stored(SourceTransaction s, TransactionStatus status = TransactionStatus.Active)
    {
        var r = Reconcile([], s);
        var t = r.NewTransactions.Single();
        t.Status = status;
        t.AuditEntries.Clear();
        return t;
    }

    [Fact]
    public void New_items_are_inserted_with_a_created_audit()
    {
        var r = Reconcile([], Tx(1), Tx(2));

        Assert.Equal(2, r.Inserted);
        Assert.All(r.NewTransactions, t =>
        {
            Assert.Equal(TransactionStatus.Active, t.Status);
            Assert.Equal(1, t.Revision);
            Assert.Equal(AuditAction.Created, Assert.Single(t.AuditEntries).Action);
        });
    }

    [Fact]
    public void Identical_snapshot_produces_no_changes()
    {
        var stored = Stored(Tx(1));

        var r = Reconcile([stored], Tx(1));

        Assert.False(r.HasChanges);
        Assert.Equal(1, r.Unchanged);
        Assert.Empty(stored.AuditEntries);
        Assert.Equal(1, stored.Revision);
    }

    [Fact]
    public void Changed_fields_are_updated_with_one_audit_per_field()
    {
        var stored = Stored(Tx(1, amount: 10m, product: "Widget"));

        var r = Reconcile([stored], Tx(1, amount: 12.5m, product: "Widget Pro"));

        Assert.Equal(1, r.Updated);
        Assert.Equal(12.5m, stored.Amount);
        Assert.Equal("Widget Pro", stored.ProductName);
        Assert.Equal(2, stored.Revision);
        Assert.Collection(stored.AuditEntries.OrderBy(a => a.FieldName),
            a => { Assert.Equal("Amount", a.FieldName); Assert.Equal("10.00", a.OldValue); Assert.Equal("12.50", a.NewValue); },
            a => { Assert.Equal("ProductName", a.FieldName); Assert.Equal("Widget", a.OldValue); Assert.Equal("Widget Pro", a.NewValue); });
        Assert.All(stored.AuditEntries, a => Assert.Equal(AuditAction.Updated, a.Action));
    }

    [Fact]
    public void Missing_item_inside_window_is_revoked()
    {
        var stored = Stored(Tx(1, hoursAgo: 23));

        var r = Reconcile([stored]);

        Assert.Equal(1, r.Revoked);
        Assert.Equal(TransactionStatus.Revoked, stored.Status);
        var audit = Assert.Single(stored.AuditEntries);
        Assert.Equal(AuditAction.Revoked, audit.Action);
        Assert.Equal("Active", audit.OldValue);
        Assert.Equal("Revoked", audit.NewValue);
    }

    [Fact]
    public void Missing_item_outside_window_is_finalized_not_revoked()
    {
        var stored = Stored(Tx(1, hoursAgo: 25));

        var r = Reconcile([stored]);

        Assert.Equal(0, r.Revoked);
        Assert.Equal(1, r.Finalized);
        Assert.Equal(TransactionStatus.Finalized, stored.Status);
    }

    [Fact]
    public void Finalized_transactions_are_immutable()
    {
        var stored = Stored(Tx(1, amount: 10m, hoursAgo: 30), TransactionStatus.Finalized);

        var r = Reconcile([stored], Tx(1, amount: 99m, hoursAgo: 30));

        Assert.False(r.HasChanges);
        Assert.Equal(10m, stored.Amount);
        Assert.Single(r.Warnings);
    }

    [Fact]
    public void Revoked_transaction_that_reappears_is_reactivated()
    {
        var stored = Stored(Tx(1), TransactionStatus.Revoked);

        var r = Reconcile([stored], Tx(1));

        Assert.Equal(1, r.Reactivated);
        Assert.Equal(TransactionStatus.Active, stored.Status);
        Assert.Equal(AuditAction.Reactivated, Assert.Single(stored.AuditEntries).Action);
    }

    [Fact]
    public void Late_arrival_older_than_window_is_inserted_as_finalized()
    {
        var r = Reconcile([], Tx(1, hoursAgo: 48));

        var t = Assert.Single(r.NewTransactions);
        Assert.Equal(TransactionStatus.Finalized, t.Status);
    }

    [Fact]
    public void Window_boundary_exactly_24h_is_still_revocable()
    {
        var stored = Stored(Tx(1, hoursAgo: 24));

        Reconcile([stored]);

        Assert.Equal(TransactionStatus.Revoked, stored.Status);
    }
}
