using System.Text.Json;
using System.Text.Json.Serialization;
using Transactions.Core.Sources;

namespace Transactions.Infrastructure.Sources;

/// <summary>
/// Mock source system backed by a JSON file that is re-read on every fetch, so you can edit it
/// between imports to simulate corrections, late arrivals and removals.
///
/// Each item uses the exercise's JSON contract. Instead of an absolute <c>transactionTime</c>, an item may
/// give <c>ageMinutes</c>: the time is then computed relative to the <b>start of the current hour</b>.
/// That keeps the demo data inside the 24h window whenever you run it, while repeated imports within the
/// same hour see byte-identical data (idempotency is observable).
/// </summary>
public sealed class JsonFileTransactionSource(string name, string filePath, bool simulateFailure, TimeProvider clock)
    : ITransactionSource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public string Name { get; } = name;

    public async Task<IReadOnlyList<SourceTransaction>> FetchSnapshotAsync(CancellationToken cancellationToken)
    {
        if (simulateFailure)
            throw new SourceSystemUnavailableException(Name, "simulated outage (SimulateFailure=true in appsettings)");

        if (!File.Exists(filePath))
            throw new SourceSystemUnavailableException(Name, $"snapshot file not found: {filePath}");

        await using var stream = File.OpenRead(filePath);
        var items = await JsonSerializer.DeserializeAsync<List<SnapshotItem>>(stream, JsonOptions, cancellationToken)
                    ?? [];

        var now = clock.GetUtcNow().UtcDateTime;
        var hourAnchor = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);

        return items.Select(i => new SourceTransaction(
                i.SourceSystem ?? string.Empty,
                i.TransactionId,
                i.CardNumber,
                i.LocationCode ?? string.Empty,
                i.ProductName ?? string.Empty,
                i.Amount,
                i.TransactionTime ?? hourAnchor.AddMinutes(-(i.AgeMinutes ?? 0))))
            .ToList();
    }

    private sealed record SnapshotItem(
        string? SourceSystem,
        int TransactionId,
        string? CardNumber,
        string? LocationCode,
        string? ProductName,
        decimal Amount,
        DateTime? TransactionTime,
        int? AgeMinutes);
}

public sealed class SourceSystemUnavailableException(string source, string reason)
    : Exception($"Source system '{source}' is unavailable: {reason}");
