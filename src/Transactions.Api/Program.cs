using System.Text.Json.Serialization;
using Transactions.Api.Endpoints;
using Transactions.Infrastructure;
using Transactions.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransactionsIngest(builder.Configuration, builder.Environment.ContentRootPath);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

const string FrontendCors = "frontend";
builder.Services.AddCors(o => o.AddPolicy(FrontendCors, p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Unhandled exceptions -> RFC 7807 problem+json (no stack traces leak outside Development).
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
    app.MapOpenApi(); // /openapi/v1.json

app.UseCors(FrontendCors);

app.MapGet("/", () => Results.Redirect("/openapi/v1.json")).ExcludeFromDescription();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Health");
app.MapImportEndpoints();
app.MapTransactionEndpoints();

await app.Services.InitializeDatabaseAsync();
await app.RunAsync();

public partial class Program; // for WebApplicationFactory in tests
