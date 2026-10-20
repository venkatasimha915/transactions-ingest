using Transactions.Core.Domain;
using Transactions.Core.Sources;

namespace Transactions.Core.Import;

/// <summary>
/// Validates and normalizes a raw snapshot before reconciliation so that comparisons are stable
/// (the same source data always normalizes to the same values, which is what makes imports idempotent).
/// </summary>
public static class SnapshotNormalizer
{
    public sealed record Result(
        IReadOnlyList<SourceTransaction> Accepted,
        IReadOnlyList<string> Rejections,
        IReadOnlyList<string> Warnings);

    public static Result Normalize(string sourceName, IEnumerable<SourceTransaction> raw)
    {
        var accepted = new Dictionary<int, SourceTransaction>();
        var rejections = new List<string>();
        var warnings = new List<string>();

        foreach (var item in raw)
        {
            if (item is null)
            {
                rejections.Add("null item");
                continue;
            }

            var key = $"{sourceName}/{item.TransactionId}";

            if (!string.Equals(item.SourceSystem?.Trim(), sourceName, StringComparison.Ordinal))
            {
                rejections.Add($"{key}: sourceSystem '{item.SourceSystem}' does not match the source it came from");
                continue;
            }

            if (item.TransactionId <= 0)
            {
                rejections.Add($"{key}: transactionId must be positive");
                continue;
            }

            var card = (item.CardNumber ?? string.Empty).Trim();
            var location = (item.LocationCode ?? string.Empty).Trim();
            var product = (item.ProductName ?? string.Empty).Trim();

            // Identifying fields must fit; we never silently change them.
            if (card.Length > Transaction.MaxTextLength || location.Length > Transaction.MaxTextLength || location.Length == 0)
            {
                rejections.Add($"{key}: cardNumber/locationCode missing or longer than {Transaction.MaxTextLength} characters");
                continue;
            }

            // ProductName is descriptive only, so we truncate rather than drop a real payment.
            // (The sample "USB-C Docking Station" is 21 characters.) Truncation is deterministic -> still idempotent.
            if (product.Length > Transaction.MaxTextLength)
            {
                warnings.Add($"{key}: productName '{product}' truncated to {Transaction.MaxTextLength} characters");
                product = product[..Transaction.MaxTextLength];
            }

            if (accepted.ContainsKey(item.TransactionId))
            {
                rejections.Add($"{key}: duplicate transactionId in snapshot; first occurrence kept");
                continue;
            }

            accepted[item.TransactionId] = item with
            {
                SourceSystem = sourceName,
                CardNumber = card,
                LocationCode = location,
                ProductName = product,
                Amount = decimal.Round(item.Amount, 2, MidpointRounding.AwayFromZero),
                TransactionTime = ToUtc(item.TransactionTime)
            };
        }

        return new Result(accepted.Values.ToList(), rejections, warnings);
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc) // unspecified: the contract says UTC
    };
}
