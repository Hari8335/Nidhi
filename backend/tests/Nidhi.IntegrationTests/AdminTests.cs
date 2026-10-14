using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nidhi.Application.Authentication;
using Nidhi.Application.Wallet;
using Nidhi.Application.Admin;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Identity;
using Nidhi.Infrastructure.Persistence;
using Nidhi.IntegrationTests.Persistence;
using Xunit;

namespace Nidhi.IntegrationTests;

public sealed class AdminTests(AuthPostgresFixture fixture) : IClassFixture<AuthPostgresFixture>
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

    private async Task<(HttpClient Client, Guid Id)> Admin()
    {
        var email = $"admin-{Key()}@example.test"; var password = $"Aa1!{Key()}";
        using (var scope = fixture.Factory.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<AdminProvisioner>().ProvisionAsync(email, password));
        var c = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await Post(c, "/api/v1/auth/login", new LoginRequest(email, password))).StatusCode);
        return (c, (await c.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me"))!.UserId);
    }
    private static async Task Problem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
    private static async Task<PricePublication> Publish(HttpClient c, decimal value = 30000m)
    {
        var response = await Post(c, "/api/v1/admin/prices", new PublishPriceRequest(value, "Integration simulation"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PricePublication>())!;
    }
    [PostgresFact]
    public async Task Anonymous_customer_and_admin_authorization_are_separate_on_every_route()
    {
        var anonymous = fixture.Factory.CreateClient(); var (customer, _) = await Customer(funds: 0); var (admin, _) = await Admin();
        foreach (var path in new[] { "dashboard", "customers", "transactions", "prices", "audit-events", $"customers/{Key()}", $"transactions/{Key()}", $"transactions/{Key()}/ledger" }) {
            await Problem(await anonymous.GetAsync("/api/v1/admin/" + path), HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED");
            await Problem(await customer.GetAsync("/api/v1/admin/" + path), HttpStatusCode.Forbidden, "FORBIDDEN");
        }
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/dashboard")).StatusCode);
        await Problem(await Post(customer, "/api/v1/admin/prices", new PublishPriceRequest(1m, "x")), HttpStatusCode.Forbidden, "FORBIDDEN");
        await Problem(await Post(anonymous, "/api/v1/admin/prices", new PublishPriceRequest(1m, "x")), HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED");
        foreach (var path in new[] { "/api/v1/customer/wallet/fundings", PurchasePath })
            await Problem(await Post(admin, path, new { amountLkr = 100m, priceVersionId = Guid.NewGuid() }, Key()), HttpStatusCode.Forbidden, "FORBIDDEN");
    }
    [PostgresFact]
    public async Task Customer_projection_search_pagination_and_missing_detail_are_safe()
    {
        var (admin, aid) = await Admin(); var (customer, id) = await Customer(funds: 250m);
        var profile = await Db(db => db.Users.Where(u => u.Id == id).Select(u => u.Email).SingleAsync());
        var page = (await admin.GetFromJsonAsync<AdminPage<CustomerSummary>>("/api/v1/admin/customers?search=" + Uri.EscapeDataString(profile!)))!;
        Assert.Equal(id, Assert.Single(page.Items).Id);
        Assert.Single((await admin.GetFromJsonAsync<AdminPage<CustomerSummary>>($"/api/v1/admin/customers?search={id.ToString()[..16]}"))!.Items);
        var detail = (await admin.GetFromJsonAsync<CustomerDetail>($"/api/v1/admin/customers/{id}"))!;
        Assert.True(detail.IsEmailVerified); Assert.Equal(250m, detail.WalletBalanceLkr); Assert.Equal(0m, detail.GoldHoldingGrams);
        var json = await admin.GetStringAsync($"/api/v1/admin/customers/{id}");
        foreach (var secret in new[] { "password", "stamp", "token", "cookie", "connection" }) Assert.DoesNotContain(secret, json, StringComparison.OrdinalIgnoreCase);
        var empty = (await admin.GetFromJsonAsync<AdminPage<CustomerSummary>>("/api/v1/admin/customers?page=2147483647"))!; Assert.Empty(empty.Items);
        await Problem(await admin.GetAsync($"/api/v1/admin/customers/{aid}"), HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND");
        var dashboard = (await admin.GetFromJsonAsync<AdminDashboard>("/api/v1/admin/dashboard"))!;
        Assert.Equal(await Db(db => db.CustomerProfiles.CountAsync()), dashboard.TotalCustomersCount);
        Assert.Equal(await Db(db => db.FinancialTransactions.CountAsync()), dashboard.TotalCompletedTransactionsCount);
    }
    [PostgresFact]
    public async Task Invalid_pagination_and_contract_filters_return_validation_errors()
    {
        var (admin, _) = await Admin();
        foreach (var path in new[] { "customers?page=0", "customers?pageSize=101", "prices?page=x", "audit-events?pageSize=0", "transactions?type=FAILED", "transactions?customerId=bad", "audit-events?actorId=bad" })
            await Problem(await admin.GetAsync("/api/v1/admin/" + path), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }
    [PostgresFact]
    public async Task Publication_preserves_history_exact_precision_actor_and_one_atomic_audit()
    {
        var (admin, id) = await Admin(); var old = await Publish(admin, 25000.1234m); var next = await Publish(admin, 26000m);
        Assert.NotEqual(old.PriceVersionId, next.PriceVersionId); Assert.Equal(id, next.PublishedByAdminId);
        await Db(async db => {
            var row = await db.GoldPrices.SingleAsync(p => p.PriceVersionId == old.PriceVersionId);
            Assert.Equal(old.PricePerGramLkr, row.PricePerGramLkr);
            Assert.Equal(old.PublishedAtUtc, row.PublishedAtUtc);
            var audit = Assert.Single(await db.AuditEvents.Where(a => a.EntityId == next.PriceVersionId.ToString()).ToListAsync());
            Assert.Equal(id, audit.ActorId); Assert.Equal("ADMIN", audit.ActorRole); Assert.Equal("PUBLISH_PRICE", audit.Action);
            Assert.Equal(audit.EventId, (await db.GoldPrices.SingleAsync(p => p.PriceVersionId == next.PriceVersionId)).AuditLogId);
            Assert.Equal(DateTimeKind.Utc, audit.TimestampUtc.Kind); return true;
        });
        var current = (await admin.GetFromJsonAsync<AdminDashboard>("/api/v1/admin/dashboard"))!.ActiveGoldPrice!;
        Assert.Equal(next.PriceVersionId, current.PriceVersionId);
        var history = (await admin.GetFromJsonAsync<AdminPage<PricePublication>>("/api/v1/admin/prices?pageSize=1"))!;
        Assert.Equal(next.PriceVersionId, Assert.Single(history.Items).PriceVersionId);
    }
    [PostgresFact]
    public async Task Invalid_prices_scale_range_reason_and_authoritative_fields_are_rejected()
    {
        var (admin, _) = await Admin(); var count = await Db(db => db.GoldPrices.CountAsync());
        foreach (var price in new[] { 0m, -1m, 25000.12345m, 1.00000m, 100000000000000m, decimal.MaxValue })
            await Problem(await Post(admin, "/api/v1/admin/prices", new PublishPriceRequest(price, "test")), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        foreach (var reason in new[] { "", " ", new string('x', 501) })
            await Problem(await Post(admin, "/api/v1/admin/prices", new PublishPriceRequest(1m, reason)), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        await Problem(await Post(admin, "/api/v1/admin/prices", new { pricePerGramLkr = 1m, reason = "x", publishedAtUtc = DateTime.UtcNow }), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        Assert.Equal(count, await Db(db => db.GoldPrices.CountAsync()));
        Assert.Equal(99999999999999.9999m, (await Publish(admin, 99999999999999.9999m)).PricePerGramLkr);
    }
    [PostgresFact]
    public async Task Publication_requires_csrf_and_admin_reads_are_no_store()
    {
        var (admin, _) = await Admin();
        await Problem(await admin.PostAsJsonAsync("/api/v1/admin/prices", new PublishPriceRequest(1m, "test")), HttpStatusCode.BadRequest, "CSRF_VALIDATION_FAILED");
        Assert.True((await admin.GetAsync("/api/v1/admin/prices")).Headers.CacheControl!.NoStore);
    }
    [PostgresFact]
    public async Task Admin_publication_connects_to_actual_customer_PRICE_CHANGED_and_reconfirmation()
    {
        var (admin, _) = await Admin(); var (customer, id) = await Customer(funds: 1000m);
        var a = await Publish(admin, 100m); var displayed = (await customer.GetFromJsonAsync<GoldPriceResponse>("/api/v1/customer/gold-price"))!;
        Assert.Equal(a.PriceVersionId, displayed.PriceVersionId); var b = await Publish(admin, 200m);
        var before = await Db(db => db.FinancialTransactions.CountAsync(t => t.CustomerId == id));
        await Problem(await Post(customer, PurchasePath, new GoldPurchaseRequest(100m, a.PriceVersionId), Key()), HttpStatusCode.Conflict, "PRICE_CHANGED");
        Assert.Equal(before, await Db(db => db.FinancialTransactions.CountAsync(t => t.CustomerId == id)));
        Assert.Equal(1000m, await Db(db => db.Wallets.Where(w => w.CustomerId == id).Select(w => w.BalanceLkr).SingleAsync()));
        Assert.Equal(0m, await Db(db => db.GoldHoldings.Where(h => h.CustomerId == id).Select(h => h.QuantityGrams).SingleAsync()));
        var response = await Post(customer, PurchasePath, new GoldPurchaseRequest(100m, b.PriceVersionId), Key()); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var receipt = (await response.Content.ReadFromJsonAsync<GoldPurchaseReceiptResponse>())!; Assert.Equal(.5m, receipt.GoldQuantityGrams);
        await Publish(admin, 300m);
        var detail = (await admin.GetFromJsonAsync<AdminTransaction>($"/api/v1/admin/transactions/{receipt.TransactionId}"))!;
        Assert.Equal(b.PriceVersionId, detail.PriceVersionId); Assert.Equal(200m, detail.AppliedPricePerGramLkr); Assert.Equal(900m, detail.PostOperationWalletBalanceLkr);
        Assert.Equal(receipt.ConversionResidualLkr, detail.ConversionResidualLkr);
        var page = (await admin.GetFromJsonAsync<AdminPage<AdminTransaction>>($"/api/v1/admin/transactions?customerId={id}"))!;
        Assert.Equal(2, page.TotalCount); Assert.Contains(page.Items, t => t.Type == "WALLET_FUNDING"); Assert.Contains(page.Items, t => t.Type == "GOLD_PURCHASE");
        var filtered = (await admin.GetFromJsonAsync<AdminPage<AdminTransaction>>($"/api/v1/admin/transactions?customerId={id}&type=GOLD_PURCHASE"))!; Assert.Single(filtered.Items);
    }
    [PostgresFact]
    public async Task Funding_and_purchase_ledger_use_persisted_accounts_and_separate_balanced_books()
    {
        var (admin, _) = await Admin(); var (customer, id) = await Customer(funds: 1000m); var price = await Publish(admin, 100m);
        var r = await Post(customer, PurchasePath, new GoldPurchaseRequest(100m, price.PriceVersionId), Key()); Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var items = (await admin.GetFromJsonAsync<AdminPage<AdminTransaction>>($"/api/v1/admin/transactions?customerId={id}"))!.Items;
        foreach (var t in items) {
            var ledger = (await admin.GetFromJsonAsync<TransactionLedger>($"/api/v1/admin/transactions/{t.TransactionId}/ledger"))!;
            Assert.Equal(t.TransactionId, ledger.TransactionId); Assert.True(ledger.LkrBook.IsBalanced); Assert.True(ledger.GoldBook.IsBalanced);
            Assert.Equal(2, ledger.LkrBook.Entries.Count); Assert.All(ledger.LkrBook.Entries, e => { Assert.Equal("LKR", e.Unit); Assert.Equal(t.AmountLkr, e.Amount); });
            Assert.Contains(ledger.LkrBook.Entries, e => e.AccountNumber == $"2001-LKR-CUST-{id}");
            if (t.Type == "GOLD_PURCHASE") { Assert.Equal(2, ledger.GoldBook.Entries.Count); Assert.All(ledger.GoldBook.Entries, e => { Assert.Equal("GOLD_GRAMS", e.Unit); Assert.Equal(t.GoldQuantityGrams, e.Amount); }); }
            else Assert.Empty(ledger.GoldBook.Entries);
        }
        await Problem(await admin.GetAsync($"/api/v1/admin/transactions/{Key()}/ledger"), HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND");
    }
    [PostgresFact]
    public async Task Concurrent_publications_both_commit_and_current_selection_matches_customer_and_history()
    {
        var (admin, _) = await Admin(); var (customer, _) = await Customer(funds: 0); var token = await Token(admin);
        var responses = await Task.WhenAll(Post(admin, "/api/v1/admin/prices", new PublishPriceRequest(11m, "a"), token: token), Post(admin, "/api/v1/admin/prices", new PublishPriceRequest(12m, "b"), token: token));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var ids = new List<Guid>(); foreach (var r in responses) ids.Add((await r.Content.ReadFromJsonAsync<PricePublication>())!.PriceVersionId);
        Assert.Equal(2, await Db(db => db.GoldPrices.CountAsync(p => ids.Contains(p.PriceVersionId))));
        var expected = await Db(db => db.GoldPrices.OrderByDescending(p => p.PublishedAtUtc).ThenByDescending(p => p.PriceVersionId).Select(p => p.PriceVersionId).FirstAsync());
        Assert.Equal(expected, (await customer.GetFromJsonAsync<GoldPriceResponse>("/api/v1/customer/gold-price"))!.PriceVersionId);
        Assert.Equal(expected, (await admin.GetFromJsonAsync<AdminDashboard>("/api/v1/admin/dashboard"))!.ActiveGoldPrice!.PriceVersionId);
        Assert.Equal(expected, (await admin.GetFromJsonAsync<AdminPage<PricePublication>>("/api/v1/admin/prices"))!.Items[0].PriceVersionId);
    }
    [PostgresFact]
    public async Task Audit_is_filtered_paginated_ordered_and_unknown_metadata_is_hidden()
    {
        var (admin, id) = await Admin(); var price = await Publish(admin);
        await Db(async db => { db.AuditEvents.Add(new AuditEvent(id, "ADMIN", "UNKNOWN", "Test", "test", "{\"password\":\"sensitive-test-value\"}", DateTime.UtcNow)); await db.SaveChangesAsync(); return true; });
        var page = (await admin.GetFromJsonAsync<AdminPage<AdminAudit>>($"/api/v1/admin/audit-events?actorId={id}&action=PUBLISH_PRICE&pageSize=1"))!;
        Assert.Equal(price.PriceVersionId.ToString(), Assert.Single(page.Items).EntityId); Assert.Equal(1, page.TotalCount);
        Assert.Equal(price.PriceVersionId.ToString(), page.Items[0].Details.GetProperty("priceVersionId").GetString());
        var unknown = (await admin.GetFromJsonAsync<AdminPage<AdminAudit>>($"/api/v1/admin/audit-events?actorId={id}&action=UNKNOWN"))!;
        Assert.Equal("{}", Assert.Single(unknown.Items).Details.GetRawText());
        Assert.Empty((await admin.GetFromJsonAsync<AdminPage<AdminAudit>>("/api/v1/admin/audit-events?page=2147483647"))!.Items);
    }
    [PostgresFact]
    public async Task Historical_records_have_no_mutation_routes()
    {
        var (admin, _) = await Admin(); var price = await Publish(admin);
        foreach (var path in new[] { $"prices/{price.PriceVersionId}", $"transactions/{Key()}", $"transactions/{Key()}/ledger", $"audit-events/{Key()}" })
            foreach (var method in new[] { HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete }) {
                using var request = new HttpRequestMessage(method, "/api/v1/admin/" + path); request.Headers.Add("X-CSRF-TOKEN", await Token(admin));
                var response = await admin.SendAsync(request); Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
            }
        Assert.Equal(price.PricePerGramLkr, await Db(db => db.GoldPrices.Where(p => p.PriceVersionId == price.PriceVersionId).Select(p => p.PricePerGramLkr).SingleAsync()));
    }
    [PostgresFact]
    public async Task No_price_returns_null_dashboard_and_customer_unavailable_without_fallback()
    {
        var isolated = new AuthPostgresFixture(); await isolated.InitializeAsync();
        try {
        var tests = new AdminTests(isolated); var (admin, _) = await tests.Admin(); var (customer, _) = await tests.Customer(funds: 0);
        Assert.Null((await admin.GetFromJsonAsync<AdminDashboard>("/api/v1/admin/dashboard"))!.ActiveGoldPrice);
        await Problem(await customer.GetAsync("/api/v1/customer/gold-price"), HttpStatusCode.ServiceUnavailable, "PRICE_UNAVAILABLE");
        var price = await Publish(admin, .0001m);
        Assert.Equal(price.PriceVersionId, (await customer.GetFromJsonAsync<GoldPriceResponse>("/api/v1/customer/gold-price"))!.PriceVersionId);
        } finally { await isolated.DisposeAsync(); }
    }
    [PostgresFact]
    public async Task Equal_publication_timestamps_use_UUID_tie_break_everywhere()
    {
        var (admin, id) = await Admin(); var (customer, _) = await Customer(funds: 0);
        var now = DateTime.UtcNow;
        var a = new GoldPrice(100m, now, id, "Equal-time fixture A", Guid.NewGuid());
        var b = new GoldPrice(200m, now, id, "Equal-time fixture B", Guid.NewGuid());
        await Db(async db => { db.GoldPrices.AddRange(a, b); await db.SaveChangesAsync(); return true; });
        var expected = await Db(db => db.GoldPrices.OrderByDescending(p => p.PublishedAtUtc).ThenByDescending(p => p.PriceVersionId).Select(p => p.PriceVersionId).FirstAsync());
        Assert.Contains(expected, new[] { a.PriceVersionId, b.PriceVersionId });
        Assert.Equal(expected, (await admin.GetFromJsonAsync<AdminDashboard>("/api/v1/admin/dashboard"))!.ActiveGoldPrice!.PriceVersionId);
        Assert.Equal(expected, (await customer.GetFromJsonAsync<GoldPriceResponse>("/api/v1/customer/gold-price"))!.PriceVersionId);
        Assert.Equal(expected, (await customer.GetFromJsonAsync<Nidhi.Application.CustomerRecords.HoldingsResponse>("/api/v1/customer/holdings"))!.PriceVersionId);
    }
    [PostgresFact]
    public async Task Audit_insert_failure_rolls_back_price_publication()
    {
        var (admin, actor) = await Admin(); var before = await Db(db => db.GoldPrices.CountAsync());
        await Db(db => db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION session11_reject_publication_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.action = 'PUBLISH_PRICE' THEN RAISE EXCEPTION 'Injected test audit failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER session11_audit_failure BEFORE INSERT ON audit_events FOR EACH ROW EXECUTE FUNCTION session11_reject_publication_audit();
            """));
        try {
            await Problem(await Post(admin, "/api/v1/admin/prices", new PublishPriceRequest(123m, "Test atomic rollback")), HttpStatusCode.InternalServerError, "INTERNAL_ERROR");
            Assert.Equal(before, await Db(db => db.GoldPrices.CountAsync()));
            Assert.Equal(0, await Db(db => db.AuditEvents.CountAsync(a => a.ActorId == actor && a.Action == "PUBLISH_PRICE")));
        } finally {
            await Db(db => db.Database.ExecuteSqlRawAsync("DROP TRIGGER session11_audit_failure ON audit_events; DROP FUNCTION session11_reject_publication_audit();"));
        }
    }

}
