using Transactions.Core.Domain;

namespace Transactions.Api.Contracts;

public sealed record TransactionDto(
    int Id,
    string SourceSystem,
    int TransactionId,
    string CardNumber,
    string LocationCode,
    string ProductName,
    decimal Amount,
    DateTime TransactionTime,
    string Status,
    int Revision,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static TransactionDto From(Transaction t) => new(
        t.Id, t.SourceSystem, t.TransactionId, t.CardNumber, t.LocationCode, t.ProductName,
        t.Amount, t.TransactionTime, t.Status.ToString(), t.Revision, t.CreatedAtUtc, t.UpdatedAtUtc);
}

public sealed record AuditEntryDto(
    long Id,
    Guid ImportRunId,
    string Action,
    string? FieldName,
    string? OldValue,
    string? NewValue,
    DateTime ChangedAtUtc);

public sealed record TransactionDetailsDto(TransactionDto Transaction, IReadOnlyList<AuditEntryDto> AuditHistory);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record ImportRunDto(
    Guid Id,
    string Trigger,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int Inserted,
    int Updated,
    int Revoked,
    int Reactivated,
    int Finalized,
    int Unchanged,
    int Rejected,
    IReadOnlyList<string> FailedSources,
    bool Succeeded)
{
    public static ImportRunDto From(ImportRun r) => new(
        r.Id, r.Trigger, r.StartedAtUtc, r.CompletedAtUtc,
        r.Inserted, r.Updated, r.Revoked, r.Reactivated, r.Finalized, r.Unchanged, r.Rejected,
        string.IsNullOrEmpty(r.FailedSources)
            ? []
            : r.FailedSources.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
        r.Succeeded);
}

/// <summary>Query string for GET /api/transactions.</summary>
public sealed record TransactionQuery(
    string? Source = null,
    string? Status = null,
    string? Search = null,
    string? Sort = null,
    string? Direction = null,
    int Page = 1,
    int PageSize = 25);
