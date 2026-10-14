using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nidhi.Application.Authentication;
using Nidhi.Application.Wallet;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Identity;
using Nidhi.Infrastructure.Persistence;
using Nidhi.IntegrationTests.Persistence;
using Xunit;

namespace Nidhi.IntegrationTests;

public sealed class GoldPurchaseTests(AuthPostgresFixture fixture) : IClassFixture<AuthPostgresFixture>
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
    private async Task State(Guid id, decimal wallet, decimal gold, int purchases, int fundings = 1)
    {
        await Db(async db =>
        {
            Assert.Equal(wallet, (await db.Wallets.SingleAsync(x => x.CustomerId == id)).BalanceLkr);
            Assert.Equal(gold, (await db.GoldHoldings.SingleAsync(x => x.CustomerId == id)).QuantityGrams);
            var transactions = await db.FinancialTransactions.Where(x => x.CustomerId == id).ToListAsync();
            Assert.Equal(purchases, transactions.Count(x => x.Type == TransactionType.GOLD_PURCHASE));
            Assert.Equal(fundings, transactions.Count(x => x.Type == TransactionType.WALLET_FUNDING));
            var ids = transactions.Select(x => x.TransactionId).ToArray();
            var entries = await db.LedgerEntries.Where(x => ids.Contains(x.TransactionId)).ToListAsync();
            Assert.Equal(4 * purchases + 2 * fundings, entries.Count);
            Assert.Equal(2 * purchases, entries.Count(x => x.Unit == LedgerUnit.GOLD_GRAMS));
            Assert.Equal(2 * purchases + 2 * fundings, entries.Count(x => x.Unit == LedgerUnit.LKR));
            foreach (var transaction in transactions)
            {
                var rows = entries.Where(x => x.TransactionId == transaction.TransactionId).ToArray();
                Assert.Equal(transaction.Type == TransactionType.GOLD_PURCHASE ? 4 : 2, rows.Length);
                foreach (var book in rows.GroupBy(x => x.Unit))
                {
                    Assert.Equal(2, book.Count());
                    Assert.All(book, x => Assert.True(x.Amount > 0));
                    Assert.Equal(book.Where(x => x.Direction == EntryDirection.DEBIT).Sum(x => x.Amount), book.Where(x => x.Direction == EntryDirection.CREDIT).Sum(x => x.Amount));
                }
                foreach (var row in rows)
                {
                    var account = await db.LedgerAccounts.SingleAsync(x => x.AccountId == row.AccountId);
                    Assert.Equal(row.Unit, account.Unit);
                    var expected = transaction.Type == TransactionType.WALLET_FUNDING
                        ? row.Direction == EntryDirection.DEBIT ? "1001-LKR-SYS-FUNDING" : $"2001-LKR-CUST-{id}"
                        : row.Unit == LedgerUnit.LKR
                            ? row.Direction == EntryDirection.DEBIT ? $"2001-LKR-CUST-{id}" : "2002-LKR-SYS-CLEARING"
                            : row.Direction == EntryDirection.DEBIT ? "1501-GOLD-SYS-ISSUANCE" : $"2501-GOLD-CUST-{id}";
                    Assert.Equal(expected, account.AccountNumber);
                    Assert.Equal(row.Unit == LedgerUnit.LKR ? transaction.AmountLkr : transaction.GoldQuantityGrams!.Value, row.Amount);
                }
            }
            var mappings = await db.IdempotencyRecords.Where(x => x.CustomerId == id).ToListAsync();
            Assert.Equal(transactions.Count, mappings.Count);
            Assert.All(mappings, mapping =>
            {
                Assert.Equal(IdempotencyStatus.COMPLETED, mapping.Status);
                Assert.Equal(transactions.Single(t => t.IdempotencyRecordId == mapping.IdempotencyRecordId).TransactionId, mapping.ResponseTransactionId);
            });
            return true;
        });
    }

    [PostgresFact]
    public async Task Current_price_is_authoritative_persisted_publication()
    {
        var (client, id) = await Customer();
        var price = await Price(id, 31234.5678m);
        var result = (await client.GetFromJsonAsync<GoldPriceResponse>("/api/v1/customer/gold-price"))!;
        Assert.Equal(price.PriceVersionId, result.PriceVersionId);
        Assert.Equal(price.PricePerGramLkr, result.PricePerGramLkr);
        var persisted = await Db(db => db.GoldPrices.SingleAsync(x => x.PriceVersionId == price.PriceVersionId));
        Assert.Equal(persisted.PublishedAtUtc, result.PublishedAtUtc);
        Assert.True(result.IsFresh);
        Assert.True(result.IsPurchasingEnabled);
    }
    [PostgresFact]
    public async Task No_publication_returns_price_unavailable_without_mutation()
    {
        // Reuse the existing fixture for an empty disposable PostgreSQL database.
        await using var isolated = new AuthPostgresFixture();
        await isolated.InitializeAsync();
        var tests = new GoldPurchaseTests(isolated);
        var (other, otherId) = await tests.Customer();
        await Problem(await other.GetAsync("/api/v1/customer/gold-price"), HttpStatusCode.ServiceUnavailable, "PRICE_UNAVAILABLE");
        await Problem(await Buy(other, Guid.NewGuid()), HttpStatusCode.ServiceUnavailable, "PRICE_UNAVAILABLE");
        await tests.State(otherId, 1000m, 0m, 0);
    }
    [PostgresFact]
    public async Task Stale_current_price_rejected_atomically()
    {
        var (client, id) = await Customer(); var price = await Price(id, stale: true);
        await Problem(await Buy(client, price.PriceVersionId), HttpStatusCode.ServiceUnavailable, "PRICE_STALE");
        await State(id, 1000m, 0m, 0);
    }
    [PostgresFact]
    public async Task Changed_price_never_substituted_and_all_financial_state_unchanged()
    {
        var (client, id) = await Customer(); var old = await Price(id); await Price(id, 40000m);
        await Problem(await Buy(client, old.PriceVersionId), HttpStatusCode.Conflict, "PRICE_CHANGED");
        await State(id, 1000m, 0m, 0);
    }
    [PostgresFact]
    public async Task Purchase_persists_exact_receipt_and_separate_balanced_books()
    {
        var (client, id) = await Customer(); var price = await Price(id);
        var receipt = await Receipt(await Buy(client, price.PriceVersionId));
        Assert.Equal(100m, receipt.AmountLkr); Assert.Equal(price.PriceVersionId, receipt.PriceVersionId);
        Assert.Equal(30000m, receipt.AppliedPricePerGramLkr); Assert.Equal(0.00333333m, receipt.GoldQuantityGrams);
        Assert.Equal(0.0001m, receipt.ConversionResidualLkr);
        await Db(async db =>
        {
            var t = await db.FinancialTransactions.SingleAsync(x => x.TransactionId == receipt.TransactionId);
            Assert.Equal(id, t.CustomerId); Assert.Equal(t.Type.ToString(), receipt.Type); Assert.Equal(t.Status.ToString(), receipt.Status);
            Assert.Equal(t.AmountLkr, receipt.AmountLkr); Assert.Equal(t.PriceVersionId, receipt.PriceVersionId);
            Assert.Equal(t.AppliedPricePerGramLkr, receipt.AppliedPricePerGramLkr); Assert.Equal(t.GoldQuantityGrams, receipt.GoldQuantityGrams);
            Assert.Equal(t.PostOperationWalletBalanceLkr, receipt.PostOperationWalletBalanceLkr);
            Assert.Equal(t.PostOperationGoldHoldingGrams, receipt.PostOperationGoldHoldingGrams); Assert.Equal(t.CreatedAtUtc, receipt.CreatedAtUtc);
            return true;
        });
        await State(id, 900m, 0.00333333m, 1);
    }
    [PostgresFact]
    public async Task Anonymous_rejected() => await Problem(await Buy(fixture.Factory.CreateClient(), Guid.NewGuid()), HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED");
    [PostgresFact]
    public async Task Unverified_customer_rejected()
    {
        var (client, id) = await Customer(false); await Problem(await Buy(client, Guid.NewGuid()), HttpStatusCode.Forbidden, "EMAIL_NOT_VERIFIED"); await State(id, 0m, 0m, 0, 0);
    }
    [PostgresFact]
    public async Task Admin_rejected()
    {
        var email = $"admin-{Key()}@example.test"; var password = $"Aa1!{Key()}";
        using (var scope = fixture.Factory.Services.CreateScope()) Assert.True(await scope.ServiceProvider.GetRequiredService<AdminProvisioner>().ProvisionAsync(email, password));
        var client = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/v1/auth/login", new LoginRequest(email, password))).StatusCode);
        await Problem(await Buy(client, Guid.NewGuid()), HttpStatusCode.Forbidden, "FORBIDDEN");
    }
    private async Task Invalid(decimal amount)
    {
        var (client, id) = await Customer(); var price = await Price(id);
        await Problem(await Buy(client, price.PriceVersionId, amount), HttpStatusCode.BadRequest, "VALIDATION_ERROR"); await State(id, 1000m, 0m, 0);
    }
    [PostgresFact] public Task Below_minimum_rejected() => Invalid(99.99m);
    [PostgresFact] public Task Above_maximum_rejected() => Invalid(1000000.01m);
    [PostgresFact] public Task Excess_decimal_places_rejected() => Invalid(100.001m);
    [PostgresFact]
    public async Task Minimum_and_exact_remaining_balance_accepted()
    {
        var (client, id) = await Customer(funds: 100m); var price = await Price(id);
        await Receipt(await Buy(client, price.PriceVersionId)); await State(id, 0m, 0.00333333m, 1);
    }
    [PostgresFact]
    public async Task Insufficient_funds_rolls_back_and_same_key_can_retry_after_funding()
    {
        var (client, id) = await Customer(funds: 100m); var price = await Price(id); var key = Key();
        await Problem(await Buy(client, price.PriceVersionId, 200m, key), HttpStatusCode.UnprocessableEntity, "INSUFFICIENT_SIMULATED_FUNDS");
        await State(id, 100m, 0m, 0);
        Assert.False(await Db(db => db.IdempotencyRecords.AnyAsync(x => x.CustomerId == id && x.IdempotencyKey == key)));
        Assert.Equal(HttpStatusCode.Created, (await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 100m }, Key())).StatusCode);
        await Receipt(await Buy(client, price.PriceVersionId, 200m, key)); await State(id, 0m, 0.00666666m, 1, 2);
    }
    [PostgresFact]
    public async Task Database_holding_overflow_rolls_back_all_purchase_writes_and_key_remains_retryable()
    {
        var (client, id) = await Customer(); var price = await Price(id); var key = Key();
        const decimal capacity = 999999999999.99999999m;
        await Db(db => db.GoldHoldings.Where(x => x.CustomerId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.QuantityGrams, capacity)));
        // A valid request exceeds the real numeric(20,8) holding capacity during SaveChanges.
        // This exercises database rollback after the service has staged its financial mutations.
        var failed = await Buy(client, price.PriceVersionId, key: key);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await State(id, 1000m, capacity, 0);
        Assert.False(await Db(db => db.IdempotencyRecords.AnyAsync(x => x.CustomerId == id && x.IdempotencyKey == key)));
        await Db(db => db.GoldHoldings.Where(x => x.CustomerId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.QuantityGrams, 0m)));
        await Receipt(await Buy(client, price.PriceVersionId, key: key));
        await State(id, 900m, 0.00333333m, 1);
    }
    private async Task Replay(string condition)
    {
        var (client, id) = await Customer(); var price = await Price(id); var key = Key();
        var original = await Receipt(await Buy(client, price.PriceVersionId, key: key));
        if (condition == "changed") await Price(id, 40000m);
        if (condition == "stale") await Db(db => db.GoldPrices.Where(x => x.PriceVersionId == price.PriceVersionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.PublishedAtUtc, DateTime.UtcNow.AddHours(-25))));
        var replay = await Receipt(await Buy(client, price.PriceVersionId, key: key), HttpStatusCode.OK);
        Assert.Equal(original, replay); await State(id, 900m, 0.00333333m, 1);
    }
    [PostgresFact] public Task Exact_replay_returns_original_without_mutation() => Replay("same");
    [PostgresFact] public Task Replay_after_new_price_returns_original_success() => Replay("changed");
    [PostgresFact] public Task Replay_after_price_stales_returns_original_success() => Replay("stale");
    private async Task Conflict(bool changePrice)
    {
        var (client, id) = await Customer(); var price = await Price(id); var key = Key();
        await Receipt(await Buy(client, price.PriceVersionId, key: key));
        await Problem(await Buy(client, changePrice ? Guid.NewGuid() : price.PriceVersionId, changePrice ? 100m : 200m, key), HttpStatusCode.Conflict, "IDEMPOTENCY_CONFLICT");
        await State(id, 900m, 0.00333333m, 1);
    }
    [PostgresFact] public Task Same_key_different_amount_conflicts() => Conflict(false);
    [PostgresFact] public Task Same_key_different_price_conflicts() => Conflict(true);

    // Hold the actual wallet row lock while starting both HTTP commands. Neither can
    // finish until release, proving overlapping transactions rather than sequential replay.
    private async Task<HttpResponseMessage[]> Concurrent(Guid id, Func<Task<HttpResponseMessage>> first, Func<Task<HttpResponseMessage>> second)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM wallets WHERE customer_id = {id} FOR UPDATE");
        var a = first(); var b = second();
        // Observe both real PostgreSQL sessions blocked on the lock before releasing it.
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (true)
        {
            await using var connection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
            await connection.OpenAsync();
            await using var command = new Npgsql.NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%FROM wallets%FOR UPDATE%'", connection);
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) >= 2) break;
            Assert.True(DateTime.UtcNow < deadline, "Both purchase/funding requests must overlap at PostgreSQL wallet lock.");
            await Task.Delay(20);
        }
        Assert.False(a.IsCompleted); Assert.False(b.IsCompleted);
        await transaction.CommitAsync();
        return await Task.WhenAll(a, b);
    }
    [PostgresFact]
    public async Task Concurrent_identical_commands_commit_once_and_replay()
    {
        var (client, id) = await Customer(); var price = await Price(id); var key = Key(); var token = await Token(client);
        var responses = await Concurrent(id, () => Buy(client, price.PriceVersionId, key: key, token: token), () => Buy(client, price.PriceVersionId, key: key, token: token));
        var created = await Receipt(responses.Single(x => x.StatusCode == HttpStatusCode.Created));
        var replay = await Receipt(responses.Single(x => x.StatusCode == HttpStatusCode.OK), HttpStatusCode.OK);
        Assert.Equal(created, replay); await State(id, 900m, 0.00333333m, 1);
    }
    [PostgresFact]
    public async Task Concurrent_distinct_purchases_cannot_overdraw()
    {
        var (client, id) = await Customer(); var price = await Price(id); var token = await Token(client);
        var responses = await Concurrent(id, () => Buy(client, price.PriceVersionId, 800m, token: token), () => Buy(client, price.PriceVersionId, 800m, token: token));
        await Receipt(responses.Single(x => x.StatusCode == HttpStatusCode.Created));
        await Problem(responses.Single(x => x.StatusCode != HttpStatusCode.Created), HttpStatusCode.UnprocessableEntity, "INSUFFICIENT_SIMULATED_FUNDS");
        await State(id, 200m, 0.02666666m, 1);
    }
    [PostgresFact]
    public async Task Concurrent_distinct_purchases_preserve_initial_holding_and_both_increments()
    {
        var (client, id) = await Customer(); var price = await Price(id);
        await Receipt(await Buy(client, price.PriceVersionId)); var token = await Token(client);
        var responses = await Concurrent(id, () => Buy(client, price.PriceVersionId, 200m, token: token), () => Buy(client, price.PriceVersionId, 300m, token: token));
        var a = await Receipt(responses[0]); var b = await Receipt(responses[1]);
        Assert.Equal(0.00666666m, a.GoldQuantityGrams); Assert.Equal(0.01m, b.GoldQuantityGrams);
        await State(id, 400m, 0.00333333m + a.GoldQuantityGrams + b.GoldQuantityGrams, 3);
    }
    [PostgresFact]
    public async Task Concurrent_funding_and_purchase_preserve_both_operations_and_ledgers()
    {
        var (client, id) = await Customer(); var price = await Price(id); var token = await Token(client);
        var responses = await Concurrent(id, () => Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 200m }, Key(), token), () => Buy(client, price.PriceVersionId, 300m, token: token));
        Assert.Equal(HttpStatusCode.Created, responses[0].StatusCode); var purchase = await Receipt(responses[1]);
        var funding = (await responses[0].Content.ReadFromJsonAsync<FundingReceiptResponse>())!;
        Assert.True((funding.PostOperationWalletBalanceLkr == 1200m && purchase.PostOperationWalletBalanceLkr == 900m) || (purchase.PostOperationWalletBalanceLkr == 700m && funding.PostOperationWalletBalanceLkr == 900m));
        await State(id, 900m, 0.01m, 1, 2);
    }
}
