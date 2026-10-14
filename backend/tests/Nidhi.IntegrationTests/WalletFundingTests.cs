using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
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

/// <summary>
/// Integration tests for the wallet funding endpoint (POST /api/v1/customer/wallet/fundings).
/// Requires a real PostgreSQL 18 instance via NIDHI_TEST_POSTGRES environment variable.
/// Uses the same AuthPostgresFixture pattern as AuthTests.
/// </summary>
public sealed class WalletFundingTests(AuthPostgresFixture fixture) : IClassFixture<AuthPostgresFixture>
{
    // ── Test helpers ──────────────────────────────────────────────────────

    private static string NewEmail() => $"wallet-{Guid.NewGuid():N}@example.test";
    private static string Password() => $"Aa1!{Guid.NewGuid():N}";

    private static async Task<string> CsrfToken(HttpClient client)
    {
        var result = await client.GetFromJsonAsync<AntiforgeryResponse>("/api/v1/auth/antiforgery");
        return result!.Token;
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body, string? idempotencyKey = null, string? csrfToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Content = JsonContent.Create(body);
        request.Headers.Add("X-CSRF-TOKEN", csrfToken ?? await CsrfToken(client));
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    /// <summary>Registers a user, verifies their email, and logs them in. Returns authed client.</summary>
    private async Task<(HttpClient Client, Guid CustomerId)> CreateVerifiedCustomer()
    {
        var client = fixture.Factory.CreateClient();
        var email = NewEmail();
        var password = Password();
        var regResp = await Post(client, "/api/v1/auth/register", new RegisterRequest(email, password, "Wallet Test"));
        Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);
        var reg = (await regResp.Content.ReadFromJsonAsync<RegistrationResponse>())!;

        var mail = fixture.Emails.Messages.Last(m => m.Email == email && m.Purpose == "verification");
        var verifyResp = await Post(client, "/api/v1/auth/verify-email", new VerifyEmailRequest(reg.CustomerId, mail.Token));
        Assert.Equal(HttpStatusCode.OK, verifyResp.StatusCode);

        var loginResp = await Post(client, "/api/v1/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        return (client, reg.CustomerId);
    }

    private static async Task AssertProblem(HttpResponseMessage resp, HttpStatusCode status, string code)
    {
        Assert.Equal(status, resp.StatusCode);
        Assert.Equal("application/problem+json", resp.Content.Headers.ContentType!.MediaType);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    // ── 1. Verified CUSTOMER can fund wallet successfully ─────────────────

    [PostgresFact]
    public async Task Fund_Verified_Customer_Success()
    {
        var (client, customerId) = await CreateVerifiedCustomer();
        var key = Guid.NewGuid().ToString();

        var resp = await Post(client, "/api/v1/customer/wallet/fundings",
            new { amountLkr = 500.00 }, key);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var receipt = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("WALLET_FUNDING", receipt.GetProperty("type").GetString());
        Assert.Equal("COMPLETED", receipt.GetProperty("status").GetString());
        Assert.Equal(500.00m, receipt.GetProperty("amountLkr").GetDecimal());
        Assert.Equal(500.00m, receipt.GetProperty("postOperationWalletBalanceLkr").GetDecimal());
        Assert.True(receipt.TryGetProperty("transactionId", out _));
        Assert.True(receipt.TryGetProperty("createdAtUtc", out _));

        // Verify wallet balance updated in DB.
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        var wallet = await db.Wallets.SingleAsync(w => w.CustomerId == customerId);
        Assert.Equal(500.00m, wallet.BalanceLkr);
    }

    // ── 2. Amount below LKR 100 rejected ─────────────────────────────────

    [PostgresFact]
    public async Task Fund_BelowMinimum_Rejected()
    {
        var (client, _) = await CreateVerifiedCustomer();
        var resp = await Post(client, "/api/v1/customer/wallet/fundings",
            new { amountLkr = 99.99 }, Guid.NewGuid().ToString());
        await AssertProblem(resp, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    // ── 3. Amount above LKR 1,000,000 rejected ───────────────────────────

    [PostgresFact]
    public async Task Fund_AboveMaximum_Rejected()
    {
        var (client, _) = await CreateVerifiedCustomer();
        var resp = await Post(client, "/api/v1/customer/wallet/fundings",
            new { amountLkr = 1000000.01 }, Guid.NewGuid().ToString());
        await AssertProblem(resp, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    // ── 4. More than 2 decimal places rejected ────────────────────────────

    [PostgresFact]
    public async Task Fund_TooManyDecimalPlaces_Rejected()
    {
        var (client, _) = await CreateVerifiedCustomer();
        // 100.001 has 3 dp
        var resp = await Post(client, "/api/v1/customer/wallet/fundings",
            new { amountLkr = 100.001 }, Guid.NewGuid().ToString());
        await AssertProblem(resp, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    // ── 5. Resulting balance above LKR 5,000,000 rejected ────────────────

    [PostgresFact]
    public async Task Fund_ExceedsWalletCap_Rejected()
    {
        var (client, customerId) = await CreateVerifiedCustomer();

        // Seed the wallet to 4,900,000 directly via DB so we don't need 49 API calls.
        using var seedScope = fixture.Factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        var wallet = await seedDb.Wallets.SingleAsync(w => w.CustomerId == customerId);
        seedDb.Entry(wallet).Property("BalanceLkr").CurrentValue = 4_900_000.00m;
        await seedDb.SaveChangesAsync();

        // 100,000.01 would push to 5,000,000.01 — exceeds cap.
        var resp = await Post(client, "/api/v1/customer/wallet/fundings",
            new { amountLkr = 100000.01 }, Guid.NewGuid().ToString());
        await AssertProblem(resp, HttpStatusCode.UnprocessableEntity, "WALLET_LIMIT_EXCEEDED");
    }

    // ── 6. Exactly LKR 5,000,000 resulting balance is allowed ─────────────

    [PostgresFact]
    public async Task Fund_ExactlyAtWalletCap_Allowed()
    {
        var (client, customerId) = await CreateVerifiedCustomer();

        using var seedScope = fixture.Factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        var wallet = await seedDb.Wallets.SingleAsync(w => w.CustomerId == customerId);
        seedDb.Entry(wallet).Property("BalanceLkr").CurrentValue = 4_900_000.00m;
        await seedDb.SaveChangesAsync();

        // 100,000 pushes exactly to 5,000,000 — allowed.
        var resp = await Post(client, "/api/v1/customer/wallet/fundings",
            new { amountLkr = 100000.00 }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var receipt = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5_000_000.00m, receipt.GetProperty("postOperationWalletBalanceLkr").GetDecimal());
    }

    // ── 7–9. Daily limit tests ─────────────────────────────────────────────

    [PostgresFact]
    public async Task Fund_20OperationsAllowed_21stRejected_FailureDoesNotConsume()
    {
        var (client, customerId) = await CreateVerifiedCustomer();

        // First prove a failed attempt doesn't consume the daily count.
        var failResp = await Post(client, "/api/v1/customer/wallet/fundings",
            new { amountLkr = 99.99 }, Guid.NewGuid().ToString());
        await AssertProblem(failResp, HttpStatusCode.BadRequest, "VALIDATION_ERROR");

        // Complete exactly 20 successful fundings.
        for (var i = 0; i < 20; i++)
        {
            var r = await Post(client, "/api/v1/customer/wallet/fundings",
                new { amountLkr = 100.00 }, Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        }

        // 21st must fail.
        var limitResp = await Post(client, "/api/v1/customer/wallet/fundings",
            new { amountLkr = 100.00 }, Guid.NewGuid().ToString());
        await AssertProblem(limitResp, HttpStatusCode.UnprocessableEntity, "DAILY_FUNDING_LIMIT_EXCEEDED");

        // Verify exactly 20 transactions in DB.
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        var count = await db.FinancialTransactions.CountAsync(t =>
            t.CustomerId == customerId && t.Type == TransactionType.WALLET_FUNDING);
        Assert.Equal(20, count);
    }

    // ── 10. Unauthenticated request rejected ──────────────────────────────

    [PostgresFact]
    public async Task Fund_Unauthenticated_Rejected()
    {
        using var client = fixture.Factory.CreateClient();
        var resp = await Post(client, "/api/v1/customer/wallet/fundings",
            new { amountLkr = 100.00 }, Guid.NewGuid().ToString());
        await AssertProblem(resp, HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED");
    }

    // ── 11. Unverified CUSTOMER rejected ──────────────────────────────────

    [PostgresFact]
    public async Task Fund_UnverifiedCustomer_Rejected()
    {
        using var client = fixture.Factory.CreateClient();
        var email = NewEmail();
        var password = Password();
        await Post(client, "/api/v1/auth/register", new RegisterRequest(email, password));
        await Post(client, "/api/v1/auth/login", new LoginRequest(email, password));

        var resp = await Post(client, "/api/v1/customer/wallet/fundings",
            new { amountLkr = 100.00 }, Guid.NewGuid().ToString());
        await AssertProblem(resp, HttpStatusCode.Forbidden, "EMAIL_NOT_VERIFIED");
    }

    // ── 12. Dashboard readable by customer (verified) ─────────────────────

    [PostgresFact]
    public async Task Dashboard_Returns_RealWalletAndGoldData()
    {
        var (client, _) = await CreateVerifiedCustomer();
        var resp = await client.GetAsync("/api/v1/customer/dashboard");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0.00m, body.GetProperty("walletBalanceLkr").GetDecimal());
        Assert.Equal(0m, body.GetProperty("goldHoldingGrams").GetDecimal());
        Assert.True(body.TryGetProperty("recentTransactions", out var recent));
        Assert.Equal(JsonValueKind.Array, recent.ValueKind);
    }

    // ── 13. Wallet summary reflects correct caps and counts ───────────────

    [PostgresFact]
    public async Task WalletSummary_ReflectsCorrectCapsAndCounts()
    {
        var (client, _) = await CreateVerifiedCustomer();
        var resp = await client.GetAsync("/api/v1/customer/wallet");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0.00m, body.GetProperty("balanceLkr").GetDecimal());
        Assert.Equal(5000000.00m, body.GetProperty("maxBalanceCapLkr").GetDecimal());
        Assert.Equal(100.00m, body.GetProperty("minFundingAmountLkr").GetDecimal());
        Assert.Equal(1000000.00m, body.GetProperty("maxFundingAmountLkr").GetDecimal());
        Assert.Equal(0, body.GetProperty("successfulFundingCountToday").GetInt32());
        Assert.Equal(20, body.GetProperty("maxDailyFundingOperations").GetInt32());
        Assert.Equal(20, body.GetProperty("remainingFundingOperationsToday").GetInt32());
    }

    // ── 14–15. FinancialTransaction + Wallet update correctness ───────────

    [PostgresFact]
    public async Task Fund_Creates_ExactlyOne_FinancialTransaction_And_Updates_Wallet_Once()
    {
        var (client, customerId) = await CreateVerifiedCustomer();
        var key = Guid.NewGuid().ToString();

        await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 250.00 }, key);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NidhiDbContext>();

        var txns = await db.FinancialTransactions
            .Where(t => t.CustomerId == customerId)
            .ToListAsync();
        Assert.Single(txns);
        Assert.Equal(TransactionType.WALLET_FUNDING, txns[0].Type);
        Assert.Equal(TransactionStatus.COMPLETED, txns[0].Status);
        Assert.Equal(250.00m, txns[0].AmountLkr);
        Assert.Equal(250.00m, txns[0].PostOperationWalletBalanceLkr);

        var wallet = await db.Wallets.SingleAsync(w => w.CustomerId == customerId);
        Assert.Equal(250.00m, wallet.BalanceLkr);
    }

    // ── 16. Balanced LKR ledger entries created ────────────────────────────

    [PostgresFact]
    public async Task Fund_Creates_Balanced_LKR_LedgerEntries()
    {
        var (client, customerId) = await CreateVerifiedCustomer();
        var key = Guid.NewGuid().ToString();

        await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 1000.00 }, key);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NidhiDbContext>();

        var txn = await db.FinancialTransactions
            .SingleAsync(t => t.CustomerId == customerId);
        var entries = await db.LedgerEntries
            .Where(e => e.TransactionId == txn.TransactionId)
            .ToListAsync();

        Assert.Equal(2, entries.Count);
        Assert.All(entries, e =>
        {
            Assert.Equal(LedgerUnit.LKR, e.Unit);
            Assert.True(e.Amount > 0);
        });

        var debit = entries.Single(e => e.Direction == EntryDirection.DEBIT);
        var credit = entries.Single(e => e.Direction == EntryDirection.CREDIT);
        Assert.Equal(1000.00m, debit.Amount);
        Assert.Equal(1000.00m, credit.Amount);
        Assert.Equal(debit.Amount, credit.Amount); // balanced

        // Verify account units match ledger entry units.
        var debitAcct = await db.LedgerAccounts.FindAsync(debit.AccountId);
        var creditAcct = await db.LedgerAccounts.FindAsync(credit.AccountId);
        Assert.Equal(LedgerUnit.LKR, debitAcct!.Unit);
        Assert.Equal(LedgerUnit.LKR, creditAcct!.Unit);

        // Verify correct account numbers per LEDGER_MODEL.md §4.1.
        Assert.Equal("1001-LKR-SYS-FUNDING", debitAcct.AccountNumber);
        Assert.StartsWith("2001-LKR-CUST-", creditAcct.AccountNumber);
    }

    // ── 17. No GOLD_GRAMS entries in funding ledger ───────────────────────

    [PostgresFact]
    public async Task Fund_LedgerContains_NoGoldGramsEntries()
    {
        var (client, customerId) = await CreateVerifiedCustomer();
        await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 500.00 }, Guid.NewGuid().ToString());

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NidhiDbContext>();

        var txn = await db.FinancialTransactions.SingleAsync(t => t.CustomerId == customerId);
        var goldEntries = await db.LedgerEntries
            .Where(e => e.TransactionId == txn.TransactionId && e.Unit == LedgerUnit.GOLD_GRAMS)
            .ToListAsync();
        Assert.Empty(goldEntries);
    }

    // ── 18. Rollback on failure (no partial data) ─────────────────────────

    [PostgresFact]
    public async Task Fund_ValidationFailure_DoesNotCreateTransactionOrLedgerEntries()
    {
        var (client, customerId) = await CreateVerifiedCustomer();

        // Attempt invalid funding (below minimum).
        await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 50.00 }, Guid.NewGuid().ToString());

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NidhiDbContext>();

        Assert.False(await db.FinancialTransactions.AnyAsync(t => t.CustomerId == customerId));
        Assert.False(await db.IdempotencyRecords.AnyAsync(r => r.CustomerId == customerId
            && r.Status == IdempotencyStatus.IN_PROGRESS));
    }

    // ── 19. Idempotent replay returns same receipt ─────────────────────────

    [PostgresFact]
    public async Task Fund_ExactReplay_ReturnsSameReceipt_DoesNotMutateBalance()
    {
        var (client, customerId) = await CreateVerifiedCustomer();
        var key = Guid.NewGuid().ToString();

        var resp1 = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 300.00 }, key);
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);
        var receipt1 = await resp1.Content.ReadFromJsonAsync<JsonElement>();

        var resp2 = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 300.00 }, key);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        var receipt2 = await resp2.Content.ReadFromJsonAsync<JsonElement>();

        // Both receipts must be identical (same transactionId, same amounts).
        Assert.Equal(receipt1.GetProperty("transactionId").GetString(), receipt2.GetProperty("transactionId").GetString());
        Assert.Equal(receipt1.GetProperty("amountLkr").GetDecimal(), receipt2.GetProperty("amountLkr").GetDecimal());
        Assert.Equal(receipt1.GetProperty("postOperationWalletBalanceLkr").GetDecimal(),
                     receipt2.GetProperty("postOperationWalletBalanceLkr").GetDecimal());

        // Wallet must only have been incremented once.
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        var wallet = await db.Wallets.SingleAsync(w => w.CustomerId == customerId);
        Assert.Equal(300.00m, wallet.BalanceLkr);

        // Exactly one transaction in DB.
        Assert.Equal(1, await db.FinancialTransactions.CountAsync(t => t.CustomerId == customerId));

        // Exactly two ledger entries (DEBIT + CREDIT).
        var txn = await db.FinancialTransactions.SingleAsync(t => t.CustomerId == customerId);
        Assert.Equal(2, await db.LedgerEntries.CountAsync(e => e.TransactionId == txn.TransactionId));
    }

    // ── 20. Same key + different amount → IDEMPOTENCY_CONFLICT ───────────

    [PostgresFact]
    public async Task Fund_SameKey_DifferentAmount_ReturnsIdempotencyConflict()
    {
        var (client, _) = await CreateVerifiedCustomer();
        var key = Guid.NewGuid().ToString();

        var r1 = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 100.00 }, key);
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

        var r2 = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 200.00 }, key);
        await AssertProblem(r2, HttpStatusCode.Conflict, "IDEMPOTENCY_CONFLICT");
    }

    // ── 21. Pre-commit failure leaves key retryable ───────────────────────

    [PostgresFact]
    public async Task Fund_PreCommitFailure_KeyIsRetryable()
    {
        var (client, customerId) = await CreateVerifiedCustomer();
        var key = Guid.NewGuid().ToString();

        // First attempt fails (amount below minimum — pre-validation, IN_PROGRESS never inserted).
        var fail = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 50.00 }, key);
        await AssertProblem(fail, HttpStatusCode.BadRequest, "VALIDATION_ERROR");

        // Same key should work fine now with valid amount.
        var success = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 100.00 }, key);
        Assert.Equal(HttpStatusCode.Created, success.StatusCode);
    }

    // ── 22. Missing Idempotency-Key header → 400 ─────────────────────────

    [PostgresFact]
    public async Task Fund_MissingIdempotencyKey_Rejected()
    {
        var (client, _) = await CreateVerifiedCustomer();
        var resp = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 100.00 });
        await AssertProblem(resp, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    // ── 23. Concurrent requests execute business operation only once ───────

    [PostgresFact]
    public async Task Fund_ConcurrentIdenticalRequests_ExecuteOnlyOnce()
    {
        var (client, customerId) = await CreateVerifiedCustomer();
        var csrfToken = await CsrfToken(client);

        var key = Guid.NewGuid().ToString();
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => Post(client, "/api/v1/customer/wallet/fundings",
                new { amountLkr = 100.00 }, key, csrfToken))
            .ToList();

        var responses = await Task.WhenAll(tasks);
        var successCount = responses.Count(r => r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK);
        Assert.Equal(5, successCount); // All idempotent — same key means same result

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        var wallet = await db.Wallets.SingleAsync(w => w.CustomerId == customerId);
        Assert.Equal(100.00m, wallet.BalanceLkr); // Only one real funding

        var txCount = await db.FinancialTransactions.CountAsync(t => t.CustomerId == customerId);
        Assert.Equal(1, txCount);
    }

    // ── 24. Concurrent requests with different keys cannot exceed wallet cap ──

    [PostgresFact]
    public async Task Fund_ConcurrentDifferentKeys_CannotExceedWalletCap()
    {
        var (client, customerId) = await CreateVerifiedCustomer();
        var csrfToken = await CsrfToken(client);

        // Seed to 4,999,900 so only one more 100 is possible.
        using var seedScope = fixture.Factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        var wallet = await seedDb.Wallets.SingleAsync(w => w.CustomerId == customerId);
        seedDb.Entry(wallet).Property("BalanceLkr").CurrentValue = 4_999_900.00m;
        await seedDb.SaveChangesAsync();

        // Fire 5 concurrent requests for 100 each — only 1 can succeed.
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => Post(client, "/api/v1/customer/wallet/fundings",
                new { amountLkr = 100.00 }, Guid.NewGuid().ToString(), csrfToken))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        var successes = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var failures = responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity);
        Assert.Equal(1, successes);
        Assert.Equal(4, failures);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        var finalWallet = await db.Wallets.SingleAsync(w => w.CustomerId == customerId);
        Assert.Equal(5_000_000.00m, finalWallet.BalanceLkr);
    }

    // ── 25. Exact replay when wallet later reaches cap ───────────────────────

    [PostgresFact]
    public async Task Fund_ExactReplay_WhenWalletLaterAtCap_ReturnsOriginalReceipt()
    {
        var (client, customerId) = await CreateVerifiedCustomer();
        var key = Guid.NewGuid().ToString();

        // A. Original funding of 500.00 succeeds.
        var r1 = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 500.00 }, key);
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        var originalReceipt = (await r1.Content.ReadFromJsonAsync<FundingReceiptResponse>())!;
        Assert.Equal(500.00m, originalReceipt.AmountLkr);
        Assert.Equal(500.00m, originalReceipt.PostOperationWalletBalanceLkr);

        // B. Customer later reaches wallet cap.
        using var seedScope = fixture.Factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        var wallet = await seedDb.Wallets.SingleAsync(w => w.CustomerId == customerId);
        seedDb.Entry(wallet).Property("BalanceLkr").CurrentValue = 5_000_000.00m;
        await seedDb.SaveChangesAsync();

        // C. Exact replay of original key still returns original receipt (not 422).
        var replay = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 500.00 }, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayReceipt = (await replay.Content.ReadFromJsonAsync<FundingReceiptResponse>())!;
        Assert.Equal(originalReceipt.TransactionId, replayReceipt.TransactionId);
        Assert.Equal(originalReceipt.AmountLkr, replayReceipt.AmountLkr);
        Assert.Equal(originalReceipt.PostOperationWalletBalanceLkr, replayReceipt.PostOperationWalletBalanceLkr);
        Assert.Equal(originalReceipt.CreatedAtUtc, replayReceipt.CreatedAtUtc);

        // D. Wallet is not changed again (still at cap).
        using var verifyScope = fixture.Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        var currentWallet = await verifyDb.Wallets.SingleAsync(w => w.CustomerId == customerId);
        Assert.Equal(5_000_000.00m, currentWallet.BalanceLkr);
    }

    // ── 26. Exact replay when customer later reaches daily limit ─────────────

    [PostgresFact]
    public async Task Fund_ExactReplay_WhenDailyLimitLaterReached_ReturnsOriginalReceipt()
    {
        var (client, customerId) = await CreateVerifiedCustomer();
        var key = Guid.NewGuid().ToString();

        // A. Original funding succeeds.
        var r1 = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 100.00 }, key);
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        var originalReceipt = (await r1.Content.ReadFromJsonAsync<FundingReceiptResponse>())!;

        // B. Customer reaches the 20-successful-fundings daily limit (seed 19 more).
        var now = DateTime.UtcNow;
        using var seedScope = fixture.Factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        for (var i = 0; i < 19; i++)
        {
            var seedKey = Guid.NewGuid().ToString();
            var idem = new IdempotencyRecord(customerId, IdempotencyOperation.SimulateFunding, seedKey,
                "dummyhash", null, IdempotencyStatus.COMPLETED, now, now);
            seedDb.IdempotencyRecords.Add(idem);
            var tx = new FinancialTransaction(customerId, TransactionType.WALLET_FUNDING, TransactionStatus.COMPLETED,
                100.00m, null, null, null, 100.00m * (i + 2), null, idem.IdempotencyRecordId, now);
            seedDb.FinancialTransactions.Add(tx);
        }
        await seedDb.SaveChangesAsync();

        // Confirm new funding is rejected by daily limit.
        var newAttempt = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 100.00 }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, newAttempt.StatusCode);

        // C. Exact replay still returns original successful receipt.
        var replay = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 100.00 }, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayReceipt = (await replay.Content.ReadFromJsonAsync<FundingReceiptResponse>())!;
        Assert.Equal(originalReceipt.TransactionId, replayReceipt.TransactionId);

        // D. Replay did not consume another daily operation.
        using var verifyScope = fixture.Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        var totalToday = await verifyDb.FinancialTransactions.CountAsync(t => t.CustomerId == customerId && t.CreatedAtUtc >= now.Date);
        Assert.Equal(20, totalToday);
    }

    // ── 27. Transactional failure rolls back IN_PROGRESS idempotency record ───

    [PostgresFact]
    public async Task Fund_PreCommitFailureInsideTx_RollsBackInProgressRecord_KeyCanRetry()
    {
        var (client, customerId) = await CreateVerifiedCustomer();
        var key = Guid.NewGuid().ToString();

        // Seed wallet to 4,999,950 so a 100 request fails INSIDE the transaction (after IN_PROGRESS inserted).
        using (var seedScope = fixture.Factory.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<NidhiDbContext>();
            var wallet = await seedDb.Wallets.SingleAsync(w => w.CustomerId == customerId);
            seedDb.Entry(wallet).Property("BalanceLkr").CurrentValue = 4_999_950.00m;
            await seedDb.SaveChangesAsync();
        }

        // Attempt funding 100.00 -> fails WALLET_LIMIT_EXCEEDED inside tx.
        var failResp = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 100.00 }, key);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, failResp.StatusCode);

        // Verify IN_PROGRESS record was rolled back cleanly.
        using (var checkScope = fixture.Factory.Services.CreateScope())
        {
            var checkDb = checkScope.ServiceProvider.GetRequiredService<NidhiDbContext>();
            var record = await checkDb.IdempotencyRecords
                .SingleOrDefaultAsync(r => r.CustomerId == customerId && r.IdempotencyKey == key);
            Assert.Null(record); // Completely rolled back!
        }

        // Customer state changes (e.g. balance reset).
        using (var resetScope = fixture.Factory.Services.CreateScope())
        {
            var resetDb = resetScope.ServiceProvider.GetRequiredService<NidhiDbContext>();
            var wallet = await resetDb.Wallets.SingleAsync(w => w.CustomerId == customerId);
            seedDbProperty(resetDb, wallet, "BalanceLkr", 0.00m);
            await resetDb.SaveChangesAsync();
        }

        // Retrying with the same key now succeeds!
        var successResp = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 100.00 }, key);
        Assert.Equal(HttpStatusCode.Created, successResp.StatusCode);

        static void seedDbProperty(NidhiDbContext db, Wallet wallet, string prop, decimal val) =>
            db.Entry(wallet).Property(prop).CurrentValue = val;
    }

    // ── 28. Concurrent requests with different keys cannot exceed daily limit ──

    [PostgresFact]
    public async Task Fund_ConcurrentDifferentKeys_CannotExceedDailyLimit()
    {
        var (client, customerId) = await CreateVerifiedCustomer();
        var csrfToken = await CsrfToken(client);

        // Seed 19 completed fundings today.
        var now = DateTime.UtcNow;
        using var seedScope = fixture.Factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        for (var i = 0; i < 19; i++)
        {
            var seedKey = Guid.NewGuid().ToString();
            var idem = new IdempotencyRecord(customerId, IdempotencyOperation.SimulateFunding, seedKey,
                "dummyhash", null, IdempotencyStatus.COMPLETED, now, now);
            seedDb.IdempotencyRecords.Add(idem);
            var tx = new FinancialTransaction(customerId, TransactionType.WALLET_FUNDING, TransactionStatus.COMPLETED,
                100.00m, null, null, null, 100.00m * (i + 1), null, idem.IdempotencyRecordId, now);
            seedDb.FinancialTransactions.Add(tx);
        }
        await seedDb.SaveChangesAsync();

        // Fire 5 concurrent requests with different keys — only 1 can succeed.
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => Post(client, "/api/v1/customer/wallet/fundings",
                new { amountLkr = 100.00 }, Guid.NewGuid().ToString(), csrfToken))
            .ToList();

        var responses = await Task.WhenAll(tasks);
        var successes = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var failures = responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity);
        Assert.Equal(1, successes);
        Assert.Equal(4, failures);

        using var verifyScope = fixture.Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<NidhiDbContext>();
        var totalToday = await verifyDb.FinancialTransactions.CountAsync(t => t.CustomerId == customerId && t.CreatedAtUtc >= now.Date);
        Assert.Equal(20, totalToday);
    }

    // ── 29. Decimal scale rejection and normalization tests ──────────────────

    [PostgresFact]
    public async Task Fund_DecimalScaleRules_EnforcedStrictly()
    {
        var (client, _) = await CreateVerifiedCustomer();

        // 100.001 -> reject
        var r1 = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 100.001 }, Guid.NewGuid().ToString());
        await AssertProblem(r1, HttpStatusCode.BadRequest, "VALIDATION_ERROR");

        // 100.009 -> reject
        var r2 = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 100.009 }, Guid.NewGuid().ToString());
        await AssertProblem(r2, HttpStatusCode.BadRequest, "VALIDATION_ERROR");

        // 100.000 -> accepted and normalized to 100.00
        var r3 = await Post(client, "/api/v1/customer/wallet/fundings", new { amountLkr = 100.000 }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, r3.StatusCode);
        var receipt = (await r3.Content.ReadFromJsonAsync<FundingReceiptResponse>())!;
        Assert.Equal(100.00m, receipt.AmountLkr);
    }
}
