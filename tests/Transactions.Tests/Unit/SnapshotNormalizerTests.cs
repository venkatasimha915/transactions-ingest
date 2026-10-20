using Transactions.Core.Import;
using static Transactions.Tests.Support.TestData;

namespace Transactions.Tests.Unit;

public class SnapshotNormalizerTests
{
    [Fact]
    public void Rejects_items_from_another_source()
    {
        var r = SnapshotNormalizer.Normalize("Online", [Tx(1, source: "Store Uptown")]);

        Assert.Empty(r.Accepted);
        Assert.Single(r.Rejections);
    }

    [Fact]
    public void Keeps_first_of_duplicate_ids()
    {
        var r = SnapshotNormalizer.Normalize("Online", [Tx(1, amount: 1m), Tx(1, amount: 2m)]);

        Assert.Equal(1m, Assert.Single(r.Accepted).Amount);
        Assert.Single(r.Rejections);
    }

    [Fact]
    public void Truncates_long_product_name_and_warns()
    {
        var r = SnapshotNormalizer.Normalize("Online", [Tx(1, product: "USB-C Docking Station")]);

        Assert.Equal("USB-C Docking Statio", Assert.Single(r.Accepted).ProductName);
        Assert.Single(r.Warnings);
    }

    [Fact]
    public void Rejects_too_long_identifying_fields()
    {
        var r = SnapshotNormalizer.Normalize("Online", [Tx(1, location: new string('X', 21))]);

        Assert.Empty(r.Accepted);
    }

    [Fact]
    public void Normalizes_null_card_amount_and_time()
    {
        var local = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Unspecified);
        var r = SnapshotNormalizer.Normalize("Online",
            [Tx(1, card: null, amount: 10.005m) with { TransactionTime = local }]);

        var item = Assert.Single(r.Accepted);
        Assert.Equal(string.Empty, item.CardNumber);
        Assert.Equal(10.01m, item.Amount);
        Assert.Equal(DateTimeKind.Utc, item.TransactionTime.Kind);
    }
}
