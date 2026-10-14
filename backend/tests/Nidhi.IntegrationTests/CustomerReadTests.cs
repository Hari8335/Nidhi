using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nidhi.Application.Authentication;
using Nidhi.Application.Wallet;
using Nidhi.Application.CustomerRecords;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Identity;
using Nidhi.Infrastructure.Persistence;
using Nidhi.IntegrationTests.Persistence;
using Xunit;

namespace Nidhi.IntegrationTests;

public sealed class CustomerReadTests(AuthPostgresFixture fixture) : IClassFixture<AuthPostgresFixture>
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
    private static async Task<TransactionPageResponse> History(HttpClient client, string query = "") =>
        (await client.GetFromJsonAsync<TransactionPageResponse>("/api/v1/customer/transactions" + query))!;
    private static async Task<HoldingsResponse> Holding(HttpClient client) =>
        (await client.GetFromJsonAsync<HoldingsResponse>("/api/v1/customer/holdings"))!;

    [PostgresFact]
    public async Task Empty_customer_has_persisted_zero_balances_and_empty_history()
    {
        var (client, id) = await Customer(funds: 0);
        var holding = await Holding(client);
        Assert.Equal(0m, holding.QuantityGrams);
        Assert.Equal(0m, (await client.GetFromJsonAsync<WalletSummaryResponse>("/api/v1/customer/wallet"))!.BalanceLkr);
        await Db(async db => {
            Assert.Equal(holding.QuantityGrams, (await db.GoldHoldings.SingleAsync(x => x.CustomerId == id)).QuantityGrams);
            Assert.Equal(0m, (await db.Wallets.SingleAsync(x => x.CustomerId == id)).BalanceLkr);
            return true;
        });
        var history = await History(client);
        Assert.Empty(history.Items); Assert.Equal(0, history.TotalCount); Assert.Equal(0, history.TotalPages);
        Assert.Equal(1, history.Page); Assert.Equal(20, history.PageSize);
    }

    [PostgresFact]
    public async Task Holdings_matches_authoritative_projection_after_multiple_purchases()
    {
        var (client, id) = await Customer(); var price = await Price(id);
        await Receipt(await Buy(client, price.PriceVersionId, 100m));
        await Receipt(await Buy(client, price.PriceVersionId, 200m));
        var holding = await Holding(client);
        Assert.Equal(0.00999999m, holding.QuantityGrams);
        await Db(async db => {
            Assert.Equal((await db.GoldHoldings.SingleAsync(x => x.CustomerId == id)).QuantityGrams, holding.QuantityGrams);
            Assert.Equal((await db.Wallets.SingleAsync(x => x.CustomerId == id)).BalanceLkr,
                (await client.GetFromJsonAsync<WalletSummaryResponse>("/api/v1/customer/wallet"))!.BalanceLkr);
            return true;
        });
        Assert.Equal(300m, holding.IndicativePortfolioValueLkr);
        Assert.Equal(price.PriceVersionId, holding.PriceVersionId); Assert.True(holding.IsPriceFresh);
    }

    [PostgresFact]
    public async Task Holdings_reads_projection_instead_of_reconstructing_history()
    {
        var (client, id) = await Customer(funds: 0);
        // Test-only projection fixture explicitly differs from empty history.
        await Db(db => db.GoldHoldings.Where(x => x.CustomerId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.QuantityGrams, 0.12345678m)));
        Assert.Equal(0.12345678m, (await Holding(client)).QuantityGrams);
        Assert.Empty((await History(client)).Items);
    }

    [PostgresFact]
    public async Task Missing_price_retains_holding_and_null_valuation()
    {
        await using var isolated = new AuthPostgresFixture(); await isolated.InitializeAsync();
        var tests = new CustomerReadTests(isolated); var (client, _) = await tests.Customer(funds: 0);
        var holding = await Holding(client);
        Assert.Equal(0m, holding.QuantityGrams); Assert.Null(holding.IndicativePortfolioValueLkr);
        Assert.Null(holding.ActivePricePerGramLkr); Assert.Null(holding.PriceVersionId); Assert.False(holding.IsPriceFresh);
    }

    [PostgresFact]
    public async Task Stale_price_retains_quantity_but_has_no_valuation()
    {
        var (client, id) = await Customer(); var price = await Price(id, stale: true);
        var holding = await Holding(client); Assert.False(holding.IsPriceFresh);
        Assert.Null(holding.IndicativePortfolioValueLkr); Assert.Equal(price.PricePerGramLkr, holding.ActivePricePerGramLkr);
        Assert.Equal(price.PriceVersionId, holding.PriceVersionId);
    }

    [PostgresFact]
    public async Task Funding_history_and_detail_preserve_original_receipt_after_later_funding()
    {
        var (client, _) = await Customer(funds: 0);
        var first = (await (await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 1250m }, Key())).Content.ReadFromJsonAsync<FundingReceiptResponse>())!;
        await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 500m }, Key());
        var item = (await History(client)).Items.Single(x => x.TransactionId == first.TransactionId);
        Assert.Equal("WALLET_FUNDING", item.Type); Assert.Equal("COMPLETED", item.Status);
        Assert.Equal(first.AmountLkr, item.AmountLkr); Assert.Equal(first.PostOperationWalletBalanceLkr, item.PostOperationWalletBalanceLkr);
        Assert.Equal(first.CreatedAtUtc, item.CreatedAtUtc); Assert.Null(item.GoldQuantityGrams); Assert.Null(item.AppliedPricePerGramLkr);
        Assert.Equal(first, await client.GetFromJsonAsync<FundingReceiptResponse>("/api/v1/customer/transactions/" + first.TransactionId));
    }

    [PostgresFact]
    public async Task Historical_purchase_uses_price_A_after_B_becomes_current()
    {
        var (client, id) = await Customer(); var a = await Price(id, 30000m);
        var receipt = await Receipt(await Buy(client, a.PriceVersionId));
        var b = await Price(id, 40000m);
        var item = (await History(client)).Items.Single(x => x.TransactionId == receipt.TransactionId);
        Assert.Equal("GOLD_PURCHASE", item.Type); Assert.Equal("COMPLETED", item.Status);
        Assert.Equal(receipt.AmountLkr, item.AmountLkr); Assert.Equal(30000m, item.AppliedPricePerGramLkr);
        Assert.Equal(receipt.GoldQuantityGrams, item.GoldQuantityGrams);
        Assert.Equal(receipt.PostOperationWalletBalanceLkr, item.PostOperationWalletBalanceLkr);
        Assert.Equal(receipt.CreatedAtUtc, item.CreatedAtUtc);
        Assert.Equal(receipt, await client.GetFromJsonAsync<GoldPurchaseReceiptResponse>("/api/v1/customer/transactions/" + receipt.TransactionId));
        Assert.Equal(b.PriceVersionId, (await Holding(client)).PriceVersionId);
        Assert.Equal(receipt.PostOperationGoldHoldingGrams, (await Holding(client)).QuantityGrams);
    }

    [PostgresFact]
    public async Task Customer_A_history_and_holdings_never_expose_B_facts()
    {
        var (a, aid) = await Customer(funds: 1500m); var (b, bid) = await Customer(funds: 2700m);
        var price = await Price(aid);
        var ar = await Receipt(await Buy(a, price.PriceVersionId, 100m));
        var br = await Receipt(await Buy(b, price.PriceVersionId, 300m));
        var bIds = (await History(b)).Items.Select(x => x.TransactionId).ToArray();
        // A forged customerId has no authority, including paginated requests.
        var history = await History(a, $"?pageSize=1&customerId={bid}");
        Assert.Equal(2, history.TotalCount);
        Assert.All(history.Items, x => { Assert.DoesNotContain(x.TransactionId, bIds); Assert.NotEqual(br.AmountLkr, x.AmountLkr); Assert.NotEqual(br.GoldQuantityGrams, x.GoldQuantityGrams); });
        Assert.Equal(ar.PostOperationGoldHoldingGrams, (await a.GetFromJsonAsync<HoldingsResponse>($"/api/v1/customer/holdings?customerId={bid}"))!.QuantityGrams);
        Assert.Equal(1400m, (await a.GetFromJsonAsync<WalletSummaryResponse>($"/api/v1/customer/wallet?customerId={bid}"))!.BalanceLkr);
        await Db(async db => { var ids = (await History(a)).Items.Select(x => x.TransactionId).ToArray();
            Assert.All(await db.FinancialTransactions.Where(x => ids.Contains(x.TransactionId)).ToListAsync(), x => Assert.Equal(aid, x.CustomerId)); return true; });
    }

    [PostgresFact]
    public async Task Foreign_detail_and_unknown_detail_have_same_not_found_contract()
    {
        var (a, _) = await Customer(); var (b, _) = await Customer();
        var bid = (await History(b)).Items[0].TransactionId;
        foreach (var id in new[] { bid, Guid.NewGuid() })
            await Problem(await a.GetAsync("/api/v1/customer/transactions/" + id), HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND");
    }

    [PostgresFact]
    public async Task Detail_has_no_ledger_or_idempotency_internals()
    {
        var (client, _) = await Customer(); var id = (await History(client)).Items[0].TransactionId;
        var json = await client.GetStringAsync("/api/v1/customer/transactions/" + id);
        var fields = JsonDocument.Parse(json).RootElement.EnumerateObject().Select(x => x.Name).Order().ToArray();
        Assert.Equal(new[] { "amountLkr", "createdAtUtc", "postOperationWalletBalanceLkr", "status", "transactionId", "type" }.Order(), fields);
    }

    [PostgresFact]
    public async Task Newest_first_with_id_tie_break_and_pages_are_deterministic()
    {
        var (client, id) = await Customer();
        for (var i = 0; i < 4; i++) await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 100m + i }, Key());
        // Equal timestamps exercise the documented secondary ordering.
        await Db(db => db.FinancialTransactions.Where(x => x.CustomerId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, DateTime.UtcNow.Date)));
        var expected = await Db(db => db.FinancialTransactions.Where(x => x.CustomerId == id).OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.TransactionId).Select(x => x.TransactionId).ToArrayAsync());
        var p1 = await History(client, "?page=1&pageSize=2"); var p2 = await History(client, "?page=2&pageSize=2"); var p3 = await History(client, "?page=3&pageSize=2");
        Assert.Equal(expected, p1.Items.Concat(p2.Items).Concat(p3.Items).Select(x => x.TransactionId));
        Assert.Equal(5, p1.TotalCount); Assert.Equal(3, p1.TotalPages); Assert.Equal(2, p1.PageSize);
        Assert.Equal(2, p2.Page); Assert.Single(p3.Items);
    }

    [PostgresFact]
    public async Task Out_of_range_and_huge_page_return_empty_without_integer_overflow()
    {
        var (client, _) = await Customer();
        foreach (var page in new[] { 2, int.MaxValue })
        { var result = await History(client, $"?page={page}&pageSize=100"); Assert.Empty(result.Items); Assert.Equal(1, result.TotalCount); Assert.Equal(page, result.Page); }
    }

    [PostgresFact]
    public async Task Invalid_pagination_returns_validation_problem()
    {
        var (client, _) = await Customer();
        foreach (var query in new[] { "page=0", "page=-1", "page=abc", "page=2147483648", "pageSize=0", "pageSize=101", "pageSize=-1", "pageSize=1.5" })
            await Problem(await client.GetAsync("/api/v1/customer/transactions?" + query), HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    [PostgresFact]
    public async Task Dashboard_matches_first_five_history_items()
    {
        var (client, id) = await Customer(); var price = await Price(id);
        await Receipt(await Buy(client, price.PriceVersionId));
        var dashboard = (await client.GetFromJsonAsync<DashboardResponse>("/api/v1/customer/dashboard"))!;
        Assert.Equal((await History(client, "?pageSize=5")).Items, dashboard.RecentTransactions);
    }

    private static readonly string[] ReadPaths = ["/api/v1/customer/holdings", "/api/v1/customer/transactions", "/api/v1/customer/transactions/019cc111-1111-7111-8111-111111111111"];
    [PostgresFact]
    public async Task Anonymous_rejected_on_all_reads()
    {
        var client = fixture.Factory.CreateClient();
        foreach (var path in ReadPaths) await Problem(await client.GetAsync(path), HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED");
    }
    [PostgresFact]
    public async Task Unverified_customer_rejected_on_all_reads()
    {
        var (client, _) = await Customer(false);
        foreach (var path in ReadPaths) await Problem(await client.GetAsync(path), HttpStatusCode.Forbidden, "EMAIL_NOT_VERIFIED");
    }
    [PostgresFact]
    public async Task Admin_rejected_on_all_customer_reads()
    {
        var email = $"admin-{Key()}@example.test"; var password = $"Aa1!{Key()}";
        using (var scope = fixture.Factory.Services.CreateScope()) Assert.True(await scope.ServiceProvider.GetRequiredService<AdminProvisioner>().ProvisionAsync(email, password));
        var client = fixture.Factory.CreateClient(); await Post(client, "/api/v1/auth/login", new LoginRequest(email, password));
        foreach (var path in ReadPaths) await Problem(await client.GetAsync(path), HttpStatusCode.Forbidden, "FORBIDDEN");
    }

    [PostgresFact]
    public async Task Successful_reads_do_not_require_csrf_and_are_not_cached()
    {
        var (client, _) = await Customer();
        foreach (var path in ReadPaths.Take(2)) {
            var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
        }
    }
}
