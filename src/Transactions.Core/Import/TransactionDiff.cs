using System.Globalization;
using Transactions.Core.Domain;
using Transactions.Core.Sources;

namespace Transactions.Core.Import;

/// <summary>Field-by-field comparison between a stored transaction and a normalized source item.</summary>
public static class TransactionDiff
{
    public sealed record FieldChange(string Field, string? OldValue, string? NewValue);

    public static IReadOnlyList<FieldChange> Compare(Transaction current, SourceTransaction incoming)
    {
        var changes = new List<FieldChange>();

        Check(changes, nameof(Transaction.CardNumber), current.CardNumber, incoming.CardNumber ?? string.Empty);
        Check(changes, nameof(Transaction.LocationCode), current.LocationCode, incoming.LocationCode);
        Check(changes, nameof(Transaction.ProductName), current.ProductName, incoming.ProductName);
        Check(changes, nameof(Transaction.Amount), current.Amount, incoming.Amount);
        Check(changes, nameof(Transaction.TransactionTime), current.TransactionTime, incoming.TransactionTime);

        return changes;
    }

    public static void Apply(SourceTransaction incoming, Transaction target)
    {
        target.CardNumber = incoming.CardNumber ?? string.Empty;
        target.LocationCode = incoming.LocationCode;
        target.ProductName = incoming.ProductName;
        target.Amount = incoming.Amount;
        target.TransactionTime = incoming.TransactionTime;
    }

    public static string? Format(object? value) => value switch
    {
        null => null,
        DateTime d => d.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFZ", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.00", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    private static void Check<T>(List<FieldChange> changes, string field, T oldValue, T newValue)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
            changes.Add(new FieldChange(field, Format(oldValue), Format(newValue)));
    }
}
