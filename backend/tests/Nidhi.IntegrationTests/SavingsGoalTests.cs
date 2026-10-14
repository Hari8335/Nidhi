using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nidhi.Application.Authentication;
using Nidhi.Application.Wallet;
using Nidhi.Application.SavingsGoals;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Identity;
using Nidhi.Infrastructure.Persistence;
using Nidhi.IntegrationTests.Persistence;
using Xunit;

namespace Nidhi.IntegrationTests;

public sealed class SavingsGoalTests(AuthPostgresFixture fixture) : IClassFixture<AuthPostgresFixture>
{
    private const string PurchasePath = "/api/v1/customer/gold-purchases";
    private static string Key() => Guid.NewGuid().ToString();
    private async Task<T> Db<T>(Func<NidhiDbContext, Task<T>> action)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<NidhiDbContext>());
    }
    private static async Task<string> Token(HttpClient client) =>
        (await client.GetFromJsonAsync<AntiforgeryResponse>("/api/v1/auth/antiforgery"))!.Token;
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body, string? key = null, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token ?? await Token(client));
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
    private async Task<(HttpClient Client, Guid Id)> Customer(bool verified = true, decimal funds = 1000m)
    {
        var client = fixture.Factory.CreateClient();
        var email = $"gold-{Key()}@example.test";
        var password = $"Aa1!{Key()}";
        var response = await Post(client, "/api/v1/auth/register", new RegisterRequest(email, password));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<RegistrationResponse>())!.CustomerId;
        if (verified)
        {
            var mail = fixture.Emails.Messages.Last(x => x.Email == email && x.Purpose == "verification");
            Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/v1/auth/verify-email", new VerifyEmailRequest(id, mail.Token))).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/v1/auth/login", new LoginRequest(email, password))).StatusCode);
        if (verified && funds > 0)
            Assert.Equal(HttpStatusCode.Created, (await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = funds }, Key())).StatusCode);
        return (client, id);
    }
    private async Task<GoldPrice> Price(Guid publisher, decimal value = 30000m, bool stale = false)
    {
        // Tests in this class run serially and share their own disposable fixture database.
        // Age earlier publications rather than deleting price history referenced by receipts.
        return await Db(async db =>
        {
            await db.GoldPrices.ExecuteUpdateAsync(s => s.SetProperty(p => p.PublishedAtUtc, DateTime.UtcNow.AddDays(-3)));
            var price = new GoldPrice(value, DateTime.UtcNow.AddHours(stale ? -25 : 0), publisher, "Integration test publication", Guid.NewGuid());
            db.GoldPrices.Add(price);
            await db.SaveChangesAsync();
            return price;
        });
    }
    private static Task<HttpResponseMessage> Buy(HttpClient client, Guid price, decimal amount = 100m, string? key = null, string? token = null) =>
        Post(client, PurchasePath, new GoldPurchaseRequest(amount, price), key ?? Key(), token);
    private static async Task Problem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
    private static async Task<GoldPurchaseReceiptResponse> Receipt(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.Created)
    {
        Assert.Equal(status, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GoldPurchaseReceiptResponse>())!;
    }
    private const string Path = "/api/v1/customer/savings-goal";
    private static Task<HttpResponseMessage> Create(HttpClient c, decimal target = 5m, DateTime? date = null, string? token = null) => Post(c, Path, new CreateGoalRequest(target, date), token: token);
    private static async Task<GoalResponse> Goal(HttpClient c) => (await c.GetFromJsonAsync<GoalResponse>(Path))!;
    [PostgresFact]
    public async Task Empty_goal_then_exact_target_and_UTC_timestamp_persist_without_financial_mutation()
    {
        var (c, id) = await Customer(funds: 0);
        Assert.Equal("{\"goal\":null}", await c.GetStringAsync(Path));
        var date = new DateTime(2020, 1, 2, 12, 30, 0, DateTimeKind.Utc);
        Assert.Equal(HttpStatusCode.Created, (await Create(c, 123.12345678m, date)).StatusCode);
        var goal = await Goal(c); Assert.Equal(123.12345678m, goal.TargetGrams); Assert.Equal(date, goal.TargetDateUtc);
        Assert.Equal(0m, goal.ProgressPercentage); Assert.False(goal.IsCompleted); Assert.Equal(goal, await Goal(c));
        await Db(async db => {
            var persisted = await db.SavingsGoals.SingleAsync(g => g.CustomerId == id);
            Assert.Equal(goal.GoalId, persisted.GoalId); Assert.Equal(goal.TargetGrams, persisted.TargetGrams); Assert.Equal(date, persisted.TargetDateUtc);
            Assert.Equal(GoalStatus.ACTIVE, persisted.Status);
            Assert.Equal(0m, (await db.GoldHoldings.SingleAsync(h => h.CustomerId == id)).QuantityGrams);
            Assert.Equal(0m, (await db.Wallets.SingleAsync(w => w.CustomerId == id)).BalanceLkr);
            Assert.Empty(await db.FinancialTransactions.Where(t => t.CustomerId == id).ToListAsync());
            Assert.Equal(0, await db.LedgerEntries.CountAsync(e => db.FinancialTransactions.Any(t => t.TransactionId == e.TransactionId && t.CustomerId == id)));
            Assert.Equal(0, await db.IdempotencyRecords.CountAsync(r => r.CustomerId == id)); return true;
        });
    }
    [PostgresFact]
    public async Task Date_can_be_omitted_and_completed_goal_remains_active_without_reservations()
    {
        var (c, id) = await Customer(funds: 0);
        await Db(db => db.GoldHoldings.Where(h => h.CustomerId == id).ExecuteUpdateAsync(s => s.SetProperty(h => h.QuantityGrams, 6.25m)));
        await Create(c); var goal = await Goal(c);
        Assert.Null(goal.TargetDateUtc); Assert.Equal(6.25m, goal.CurrentHoldingGrams); Assert.Equal(125m, goal.ProgressPercentage);
        Assert.Equal(100m, goal.VisualProgressPercentage); Assert.True(goal.IsCompleted);
        await Problem(await Create(c), HttpStatusCode.Conflict, "ACTIVE_GOAL_ALREADY_EXISTS"); Assert.Equal(goal, await Goal(c));
    }
    [PostgresFact]
    public async Task Invalid_targets_dates_and_client_owned_fields_are_rejected()
    {
        var (c, _) = await Customer(funds: 0);
        foreach (var value in new[] { 0m, -1m, .123456789m, 1.000000000m, 1000000000000m })
            await Problem(await Create(c, value), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        foreach (var body in new[] { "{\"targetGrams\":5,\"targetDateUtc\":\"2026-01-01\"}", "{\"targetGrams\":5,\"targetDateUtc\":\"invalid\"}", "{\"targetGrams\":5,\"customerId\":\"019cc111-1111-7111-8111-111111111111\"}" }) {
            using var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
            request.Headers.Add("X-CSRF-TOKEN", await Token(c)); await Problem(await c.SendAsync(request), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        }
        Assert.Equal("{\"goal\":null}", await c.GetStringAsync(Path));
    }
    [PostgresFact]
    public async Task Maximum_precision_and_exact_decimal_string_target_are_supported()
    {
        var (c, _) = await Customer(funds: 0);
        Assert.Equal(HttpStatusCode.Created, (await Post(c, Path, new { targetGrams = "999999999999.99999999" })).StatusCode);
        Assert.Equal(999999999999.99999999m, (await Goal(c)).TargetGrams);
    }
    [PostgresFact]
    public async Task Concurrent_same_customer_creation_has_one_success_one_conflict_and_one_active_row()
    {
        var (c, id) = await Customer(funds: 0); var token = await Token(c);
        var responses = await Task.WhenAll(Create(c, 5m, token: token), Create(c, 10m, token: token));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        await Problem(Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict, "ACTIVE_GOAL_ALREADY_EXISTS");
        Assert.Equal(1, await Db(db => db.SavingsGoals.CountAsync(g => g.CustomerId == id && g.Status == GoalStatus.ACTIVE)));
    }
    [PostgresFact]
    public async Task Own_goal_isolated_from_other_customer_and_no_edit_delete_routes_exist()
    {
        var (a, _) = await Customer(funds: 0); var (b, bid) = await Customer(funds: 0); await Create(b, 10m);
        Assert.Equal("{\"goal\":null}", await a.GetStringAsync(Path + "?customerId=" + bid)); await Create(a, 5m);
        Assert.Equal(5m, (await a.GetFromJsonAsync<GoalResponse>(Path + "?customerId=" + bid))!.TargetGrams);
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Delete }) {
            using var request = new HttpRequestMessage(method, Path); request.Headers.Add("X-CSRF-TOKEN", await Token(a));
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await a.SendAsync(request)).StatusCode);
        }
        Assert.Equal(10m, (await Goal(b)).TargetGrams);
    }
    [PostgresFact]
    public async Task Progress_reads_persisted_holding_then_actual_purchase_and_dashboard_share_the_goal()
    {
        var (c, id) = await Customer(funds: 1000m);
        await Db(db => db.GoldHoldings.Where(h => h.CustomerId == id).ExecuteUpdateAsync(s => s.SetProperty(h => h.QuantityGrams, 1m)));
        await Create(c); Assert.Equal(20m, (await Goal(c)).ProgressPercentage);
        var price = await Price(id, 100m); var receipt = await Receipt(await Buy(c, price.PriceVersionId, 100m));
        var goal = await Goal(c); Assert.Equal(2m, goal.CurrentHoldingGrams); Assert.Equal(40m, goal.ProgressPercentage);
        Assert.Equal(receipt.PostOperationGoldHoldingGrams, goal.CurrentHoldingGrams);
        Assert.Equal(goal, (await c.GetFromJsonAsync<DashboardResponse>("/api/v1/customer/dashboard"))!.ActiveGoal); Assert.Equal(goal, await Goal(c));
    }
    [PostgresFact]
    public async Task Missing_holding_is_unavailable_instead_of_zero()
    {
        var (c, id) = await Customer(funds: 0); await Create(c);
        await Db(db => db.GoldHoldings.Where(h => h.CustomerId == id).ExecuteDeleteAsync());
        await Problem(await c.GetAsync(Path), HttpStatusCode.ServiceUnavailable, "HOLDING_UNAVAILABLE");
    }
    [PostgresFact]
    public async Task Anonymous_unverified_and_admin_are_rejected_on_reads_and_creates()
    {
        var anonymous = fixture.Factory.CreateClient(); var (unverified, _) = await Customer(false, 0);
        var email = $"admin-{Key()}@example.test"; var password = $"Aa1!{Key()}";
        using (var scope = fixture.Factory.Services.CreateScope()) Assert.True(await scope.ServiceProvider.GetRequiredService<AdminProvisioner>().ProvisionAsync(email, password));
        var admin = fixture.Factory.CreateClient(); await Post(admin, "/api/v1/auth/login", new LoginRequest(email, password));
        foreach (var (c, status, code) in new[] { (anonymous, HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED"), (unverified, HttpStatusCode.Forbidden, "EMAIL_NOT_VERIFIED"), (admin, HttpStatusCode.Forbidden, "FORBIDDEN") }) {
            await Problem(await c.GetAsync(Path), status, code); await Problem(await Create(c), status, code);
        }
    }
    [PostgresFact]
    public async Task Creation_requires_csrf_and_reads_are_no_store()
    {
        var (c, _) = await Customer(funds: 0);
        await Problem(await c.PostAsJsonAsync(Path, new CreateGoalRequest(5m)), HttpStatusCode.BadRequest, "CSRF_VALIDATION_FAILED");
        Assert.True((await c.GetAsync(Path)).Headers.CacheControl!.NoStore);
    }
}
