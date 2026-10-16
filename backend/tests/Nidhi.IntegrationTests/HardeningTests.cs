using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nidhi.Application.Authentication;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Identity;
using Nidhi.Infrastructure.Persistence;
using Nidhi.IntegrationTests.Persistence;
using Xunit;

namespace Nidhi.IntegrationTests;

// Only the test host adds this assembly as an MVC application part.
[ApiController]
[Route("api/hardening-tests")]
public sealed class HardeningFailureProbeController : ControllerBase
{
    [HttpGet("failure")]
    public IActionResult Failure() => throw new InvalidOperationException("private-sentinel SQL password internal/path");

    [HttpGet("cancelled")]
    public IActionResult Cancelled()
    {
        HttpContext.RequestAborted = new CancellationToken(canceled: true);
        throw new OperationCanceledException();
    }
}

public sealed class HardeningTests(AuthPostgresFixture fixture) : IClassFixture<AuthPostgresFixture>
{
    private static async Task<string> Token(HttpClient client) =>
        (await client.GetFromJsonAsync<AntiforgeryResponse>("/api/v1/auth/antiforgery"))!.Token;
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body, string? csrf = null, bool omit = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/" + path) { Content = JsonContent.Create(body) };
        if (!omit) request.Headers.Add("X-CSRF-TOKEN", csrf ?? await Token(client));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }
    private async Task<(HttpClient Client, Guid Id, string Email, string Password)> Customer()
    {
        var client = fixture.Factory.CreateClient();
        var email = $"hardening-{Guid.NewGuid():N}@example.test"; var password = $"Aa1!{Guid.NewGuid():N}";
        var registered = await Post(client, "auth/register", new RegisterRequest(email, password));
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var id = (await registered.Content.ReadFromJsonAsync<RegistrationResponse>())!.CustomerId;
        var mail = fixture.Emails.Messages.Last(x => x.UserId == id && x.Purpose == "verification");
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "auth/verify-email", new VerifyEmailRequest(id, mail.Token))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "auth/login", new LoginRequest(email, password))).StatusCode);
        return (client, id, email, password);
    }
    private async Task<HttpClient> Admin()
    {
        var email = $"hardening-admin-{Guid.NewGuid():N}@example.test"; var password = $"Aa1!{Guid.NewGuid():N}";
        using (var scope = fixture.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AdminProvisioner>().ProvisionAsync(email, password);
        var client = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "auth/login", new LoginRequest(email, password))).StatusCode);
        return client;
    }
    private static async Task<JsonElement> Problem(HttpResponseMessage response, int status, string code)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("about:blank", json.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("detail").GetString()));
        Assert.Equal(status, json.GetProperty("status").GetInt32());
        Assert.StartsWith("/api/", json.GetProperty("instance").GetString());
        Assert.Equal(code, json.GetProperty("code").GetString());
        Assert.NotEqual(default, json.GetProperty("timestampUtc").GetDateTime());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("private-sentinel", json.ToString());
        return json;
    }

    [PostgresFact]
    public async Task Missing_invalid_and_other_identity_csrf_reject_every_financial_mutation_before_writes()
    {
        var customer = await Customer(); using var admin = await Admin(); var other = await Customer();
        var foreignToken = await Token(other.Client);
        foreach (var (path, body) in new (string, object)[] {
            ("customer/wallet/fundings", new { amountLkr = 100m }),
            ("customer/gold-purchases", new { amountLkr = 100m, priceVersionId = Guid.NewGuid() }),
            ("customer/savings-goal", new { targetGrams = "1.00000000" }) })
        {
            await Problem(await Post(customer.Client, path, body, omit: true), 400, "CSRF_VALIDATION_FAILED");
            await Problem(await Post(customer.Client, path, body, "invalid"), 400, "CSRF_VALIDATION_FAILED");
            await Problem(await Post(customer.Client, path, body, foreignToken), 400, "CSRF_VALIDATION_FAILED");
        }
        var publication = new { pricePerGramLkr = "25000.0000", reason = "Hardening simulation" };
        await Problem(await Post(admin, "admin/prices", publication, omit: true), 400, "CSRF_VALIDATION_FAILED");
        await Problem(await Post(admin, "admin/prices", publication, "invalid"), 400, "CSRF_VALIDATION_FAILED");
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        Assert.Equal(0m, (await db.Wallets.SingleAsync(w => w.CustomerId == customer.Id)).BalanceLkr);
        Assert.False(await db.FinancialTransactions.AnyAsync(t => t.CustomerId == customer.Id));
        Assert.False(await db.SavingsGoals.AnyAsync(t => t.CustomerId == customer.Id));
        Assert.False(await db.IdempotencyRecords.AnyAsync(t => t.CustomerId == customer.Id));
        var priceResponse = await Post(admin, "admin/prices", publication);
        Assert.Equal(HttpStatusCode.Created, priceResponse.StatusCode);
        var version = (await priceResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("priceVersionId").GetGuid();
        Assert.Equal(HttpStatusCode.Created, (await Post(customer.Client, "customer/wallet/fundings", new { amountLkr = 100m })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Post(customer.Client, "customer/gold-purchases", new { amountLkr = 100m, priceVersionId = version })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Post(customer.Client, "customer/savings-goal", new { targetGrams = "1.00000000" })).StatusCode);
    }

    [PostgresFact]
    public async Task Decimal_extremes_are_validation_errors_without_financial_writes_or_internal_errors()
    {
        var customer = await Customer();
        foreach (var value in new[] { decimal.MaxValue, decimal.MinValue })
        {
            await Problem(await Post(customer.Client, "customer/wallet/fundings", new { amountLkr = value }), 400, "VALIDATION_ERROR");
            await Problem(await Post(customer.Client, "customer/gold-purchases", new { amountLkr = value, priceVersionId = Guid.NewGuid() }), 400, "VALIDATION_ERROR");
        }
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        Assert.Equal(0m, (await db.Wallets.SingleAsync(w => w.CustomerId == customer.Id)).BalanceLkr);
        Assert.False(await db.FinancialTransactions.AnyAsync(t => t.CustomerId == customer.Id));
    }

    [PostgresFact]
    public async Task Representative_http_failures_have_a_safe_consistent_problem_envelope()
    {
        using var anonymous = fixture.Factory.CreateClient(); var customer = await Customer(); using var admin = await Admin();
        await Problem(await Post(anonymous, "auth/register", new { email = "invalid", password = "short" }), 400, "VALIDATION_ERROR");
        await Problem(await anonymous.GetAsync("/api/v1/auth/me"), 401, "AUTHENTICATION_REQUIRED");
        await Problem(await customer.Client.GetAsync("/api/v1/admin/dashboard"), 403, "FORBIDDEN");
        await Problem(await customer.Client.GetAsync($"/api/v1/customer/transactions/{Guid.NewGuid()}"), 404, "RESOURCE_NOT_FOUND");
        await Problem(await Post(anonymous, "auth/register", new RegisterRequest(customer.Email, customer.Password)), 409, "EMAIL_ALREADY_EXISTS");
        var price = await Post(admin, "admin/prices", new { pricePerGramLkr = "26000.0000", reason = "Hardening simulation" });
        var id = (await price.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("priceVersionId").GetGuid();
        await Problem(await Post(customer.Client, "customer/gold-purchases", new { amountLkr = 100m, priceVersionId = id }), 422, "INSUFFICIENT_SIMULATED_FUNDS");
        using var limitedFactory = fixture.NewFactory(limit: 1); using var limited = limitedFactory.CreateClient();
        await Post(limited, "auth/login", new LoginRequest($"missing-{Guid.NewGuid()}@example.test", customer.Password));
        var rateLimited = await Post(limited, "auth/login", new LoginRequest(customer.Email, customer.Password));
        await Problem(rateLimited, 429, "RATE_LIMITED"); Assert.True(rateLimited.Headers.Contains("Retry-After"));
        fixture.Emails.Fail = true;
        try { await Problem(await Post(anonymous, "auth/register", new RegisterRequest($"failure-{Guid.NewGuid()}@example.test", customer.Password)), 503, "IDENTITY_EMAIL_UNAVAILABLE"); }
        finally { fixture.Emails.Fail = false; }
        var failure = await Problem(await anonymous.GetAsync("/api/hardening-tests/failure"), 500, "INTERNAL_ERROR");
        Assert.Equal("The request could not be completed.", failure.GetProperty("detail").GetString());
    }

    [PostgresFact]
    public async Task Client_cancellation_does_not_become_an_internal_server_failure()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/api/hardening-tests/cancelled");
        Assert.Equal(499, (int)response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Production", true)]
    public async Task Security_headers_and_HSTS_are_environment_scoped_without_cross_origin_credentials(string environment, bool hsts)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment(environment));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://security.example.test") });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health"); request.Headers.Add("Origin", "https://untrusted.example.test");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal(hsts, response.Headers.Contains("Strict-Transport-Security"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }
}
