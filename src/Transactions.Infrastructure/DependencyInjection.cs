using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Transactions.Core.Import;
using Transactions.Core.Sources;
using Transactions.Infrastructure.Import;
using Transactions.Infrastructure.Persistence;
using Transactions.Infrastructure.Sources;

namespace Transactions.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers everything the import needs. Any host (web API, worker service, console job) calls this
    /// and then resolves <see cref="ITransactionImportService"/> — the import logic is host-agnostic.
    /// </summary>
    public static IServiceCollection AddTransactionsIngest(
        this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<ImportOptions>()
            .Bind(configuration.GetSection(ImportOptions.SectionName))
            .Validate(o => o.ReconciliationWindowHours > 0, "Import:ReconciliationWindowHours must be positive")
            .ValidateOnStart();

        services.AddOptions<SourceSystemsOptions>()
            .Bind(configuration.GetSection(SourceSystemsOptions.SectionName))
            .Validate(o => o.Systems.Count > 0, "At least one SourceSystems:Systems entry is required")
            .Validate(o => o.Systems.All(s => !string.IsNullOrWhiteSpace(s.Name) && s.Name.Length <= Core.Domain.Transaction.MaxTextLength),
                "Every source system needs a Name of at most 20 characters")
            .Validate(o => o.Systems.Select(s => s.Name).Distinct(StringComparer.Ordinal).Count() == o.Systems.Count,
                "Source system names must be unique")
            .ValidateOnStart();

        var connectionString = configuration.GetConnectionString("Transactions")
            ?? throw new InvalidOperationException("ConnectionStrings:Transactions is not configured");
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));

        // One ITransactionSource per configured system. Swap these for real HTTP clients in production.
        var sourceConfig = configuration.GetSection(SourceSystemsOptions.SectionName).Get<SourceSystemsOptions>()
                           ?? new SourceSystemsOptions();
        var dataDirectory = Path.GetFullPath(Path.Combine(contentRootPath, sourceConfig.DataDirectory));
        foreach (var system in sourceConfig.Systems)
        {
            services.AddSingleton<ITransactionSource>(sp => new JsonFileTransactionSource(
                system.Name,
                Path.Combine(dataDirectory, system.File),
                system.SimulateFailure,
                sp.GetRequiredService<TimeProvider>()));
        }

        services.AddSingleton<ImportRunLock>();
        services.AddScoped<ITransactionImportService, TransactionImportService>();
        return services;
    }
}
