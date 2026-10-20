using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Transactions.Tests.Integration;

/// <summary>End-to-end through HTTP, using the real JSON sample sources and a throwaway SQLite file.</summary>
public class ApiTests : IClassFixture<ApiTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tx-test-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Transactions", $"Data Source={_dbPath};Pooling=False");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { File.Delete(_dbPath); } catch (IOException) { }
        }
    }

    private readonly HttpClient _client;
    public ApiTests(Factory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Import_then_list_returns_transactions()
    {
        var import = await _client.PostAsync("/api/import", null);
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);

        var page = await _client.GetFromJsonAsync<JsonElement>("/api/transactions?pageSize=100");
        Assert.True(page.GetProperty("totalCount").GetInt32() >= 20);

        var first = page.GetProperty("items")[0];
        foreach (var field in new[] { "sourceSystem", "transactionId", "locationCode", "productName", "amount", "transactionTime", "status" })
            Assert.True(first.TryGetProperty(field, out _), $"missing {field}");

        var details = await _client.GetFromJsonAsync<JsonElement>($"/api/transactions/{first.GetProperty("id").GetInt32()}");
        Assert.True(details.GetProperty("auditHistory").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Invalid_query_returns_problem_details()
    {
        var response = await _client.GetAsync("/api/transactions?status=bogus&pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Unknown_transaction_returns_404()
    {
        var response = await _client.GetAsync("/api/transactions/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
