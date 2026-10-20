using Transactions.Core.Sources;

namespace Transactions.Tests.Support;

internal static class TestData
{
    public static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    public static SourceTransaction Tx(
        int id,
        string source = "Online",
        decimal amount = 10.00m,
        string product = "Widget",
        string? card = "***1111",
        string location = "ONLINE",
        double hoursAgo = 1) =>
        new(source, id, card, location, product, amount, Now.AddHours(-hoursAgo));
}

/// <summary>In-memory source whose snapshot can be swapped between runs; can be told to fail.</summary>
internal sealed class FakeSource(string name) : ITransactionSource
{
    public string Name { get; } = name;
    public List<SourceTransaction> Snapshot { get; set; } = [];
    public bool Fail { get; set; }

    public Task<IReadOnlyList<SourceTransaction>> FetchSnapshotAsync(CancellationToken cancellationToken) =>
        Fail
            ? throw new HttpRequestException($"{Name} is down")
            : Task.FromResult<IReadOnlyList<SourceTransaction>>(Snapshot.ToList());
}
