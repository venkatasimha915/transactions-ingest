using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Transactions.Api.Contracts;
using Transactions.Core.Import;
using Transactions.Infrastructure.Persistence;

namespace Transactions.Api.Endpoints;

public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/import").WithTags("Import");

        group.MapPost("/", RunImport)
             .WithSummary("Runs the import synchronously and returns the run summary. 409 if one is already running.");

        app.MapGet("/api/imports", GetRecentRuns)
           .WithTags("Import")
           .WithSummary("Most recent import runs (newest first).");

        return app;
    }

    internal static async Task<Results<Ok<ImportRunDto>, ProblemHttpResult>> RunImport(
        ITransactionImportService importer, CancellationToken ct)
    {
        try
        {
            var run = await importer.RunAsync("manual", ct);
            return TypedResults.Ok(ImportRunDto.From(run));
        }
        catch (ImportAlreadyRunningException ex)
        {
            return TypedResults.Problem(title: "Import already running", detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    internal static async Task<Ok<List<ImportRunDto>>> GetRecentRuns(AppDbContext db, CancellationToken ct, int take = 20)
    {
        take = Math.Clamp(take, 1, 100);
        var runs = await db.ImportRuns.AsNoTracking()
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(take)
            .ToListAsync(ct);
        return TypedResults.Ok(runs.Select(ImportRunDto.From).ToList());
    }
}
