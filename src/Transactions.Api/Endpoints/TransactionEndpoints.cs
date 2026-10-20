using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Transactions.Api.Contracts;
using Transactions.Core.Domain;
using Transactions.Infrastructure.Persistence;
using Transactions.Infrastructure.Sources;

namespace Transactions.Api.Endpoints;

public static class TransactionEndpoints
{
    public const int MaxPageSize = 200;

    public static RouteGroupBuilder MapTransactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/transactions").WithTags("Transactions");

        group.MapGet("/", GetTransactions)
             .WithSummary("Paged list of imported transactions with optional filtering and sorting.");

        group.MapGet("/{id:int}", GetTransaction)
             .WithSummary("One transaction with its full audit history.");

        app.MapGet("/api/sources", (IOptions<SourceSystemsOptions> options) =>
                options.Value.Systems.Select(s => s.Name).ToArray())
           .WithTags("Transactions")
           .WithSummary("Configured source system names (for UI filters).");

        return group;
    }

    internal static async Task<Results<Ok<PagedResult<TransactionDto>>, ValidationProblem>> GetTransactions(
        [AsParameters] TransactionQuery query, AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        TransactionStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (Enum.TryParse<TransactionStatus>(query.Status, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
                status = parsed;
            else
                errors["status"] = [$"Unknown status '{query.Status}'. Use one of: {string.Join(", ", Enum.GetNames<TransactionStatus>())}."];
        }
        if (query.Page < 1) errors["page"] = ["page must be >= 1"];
        if (query.PageSize is < 1 or > MaxPageSize) errors["pageSize"] = [$"pageSize must be between 1 and {MaxPageSize}"];
        if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

        var q = db.Transactions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Source)) q = q.Where(t => t.SourceSystem == query.Source);
        if (status is not null) q = q.Where(t => t.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = int.TryParse(term, out var txId)
                ? q.Where(t => t.TransactionId == txId)
                : q.Where(t => EF.Functions.Like(t.ProductName, $"%{term}%") || EF.Functions.Like(t.LocationCode, $"%{term}%"));
        }

        var total = await q.CountAsync(ct);
        var items = await ApplySort(q, query.Sort, query.Direction)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return TypedResults.Ok(new PagedResult<TransactionDto>(
            items.Select(TransactionDto.From).ToList(), query.Page, query.PageSize, total));
    }

    internal static async Task<Results<Ok<TransactionDetailsDto>, NotFound>> GetTransaction(
        int id, AppDbContext db, CancellationToken ct)
    {
        var t = await db.Transactions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return TypedResults.NotFound();

        var audit = await db.TransactionAudits.AsNoTracking()
            .Where(a => a.TransactionRecordId == id)
            .OrderBy(a => a.ChangedAtUtc).ThenBy(a => a.Id)
            .Select(a => new AuditEntryDto(a.Id, a.ImportRunId, a.Action.ToString(), a.FieldName, a.OldValue, a.NewValue, a.ChangedAtUtc))
            .ToListAsync(ct);

        return TypedResults.Ok(new TransactionDetailsDto(TransactionDto.From(t), audit));
    }

    private static IQueryable<Transaction> ApplySort(IQueryable<Transaction> q, string? sort, string? direction)
    {
        var desc = !string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);
        // Whitelisted columns only; a stable tie-breaker keeps paging deterministic.
        IOrderedQueryable<Transaction> ordered = (sort?.ToLowerInvariant()) switch
        {
            "sourcesystem" => desc ? q.OrderByDescending(t => t.SourceSystem) : q.OrderBy(t => t.SourceSystem),
            "transactionid" => desc ? q.OrderByDescending(t => t.TransactionId) : q.OrderBy(t => t.TransactionId),
            "locationcode" => desc ? q.OrderByDescending(t => t.LocationCode) : q.OrderBy(t => t.LocationCode),
            "productname" => desc ? q.OrderByDescending(t => t.ProductName) : q.OrderBy(t => t.ProductName),
            "amount" => desc ? q.OrderByDescending(t => t.Amount) : q.OrderBy(t => t.Amount),
            "status" => desc ? q.OrderByDescending(t => t.Status) : q.OrderBy(t => t.Status),
            _ => desc ? q.OrderByDescending(t => t.TransactionTime) : q.OrderBy(t => t.TransactionTime)
        };
        return ordered.ThenBy(t => t.Id);
    }
}
