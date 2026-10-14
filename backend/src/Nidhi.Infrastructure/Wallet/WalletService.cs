using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nidhi.Application.Wallet;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Persistence;

namespace Nidhi.Infrastructure.WalletServices;


/// <summary>
/// Implements all wallet use-cases.
///
/// Funding transaction sequence (per CONCURRENCY.md §2.2 and §3.2):
/// 1. Validate input (amount range, decimal scale). Throws WalletFailure on invalid input.
/// 2. Begin PostgreSQL transaction.
/// 3. Acquire FOR UPDATE lock on customer wallet row — all concurrent fundings serialize here.
/// 4. Resolve idempotency record (inside the lock):
///    a. COMPLETED + same hash  → commit read-only → return original receipt (isReplay=true).
///    b. COMPLETED + diff hash  → rollback → 409 IDEMPOTENCY_CONFLICT.
///    c. IN_PROGRESS            → rollback → 409 OPERATION_IN_PROGRESS.
///    d. Not found              → INSERT IN_PROGRESS record, proceed.
/// 5. Re-evaluate business rules inside the lock (daily count, wallet cap).
/// 6. Mutate wallet + insert FinancialTransaction + LedgerEntries + mark idempotency COMPLETED.
/// 7. Commit atomically.
///
/// On any WalletFailure thrown after step 4-insert, the transaction is rolled back by the
/// await-using disposal, removing the IN_PROGRESS record and leaving the key retryable.
/// </summary>
public sealed class WalletService(NidhiDbContext db) : IWalletService
{
    // ── Funding business constants (per SIMULATED_WALLET_FLOW.md) ─────────

    private const decimal MinFunding = 100.00m;
    private const decimal MaxFunding = 1_000_000.00m;
    private const decimal MaxBalance = 5_000_000.00m;
    private const int MaxDailyFundings = 20;

    // Ledger account numbers per LEDGER_MODEL.md §3
    private const string SysFundingAccountNumber = "1001-LKR-SYS-FUNDING";

    // ── IWalletService ─────────────────────────────────────────────────────

    public async Task<WalletSummaryResponse> GetWalletAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var customerId = ResolveCustomerId(principal);
        var wallet = await db.Wallets.AsNoTracking()
            .SingleAsync(w => w.CustomerId == customerId, ct);

        var startOfUtcDay = DateTime.UtcNow.Date;
        var countToday = await db.FinancialTransactions.AsNoTracking()
            .CountAsync(t => t.CustomerId == customerId
                          && t.Type == TransactionType.WALLET_FUNDING
                          && t.Status == TransactionStatus.COMPLETED
                          && t.CreatedAtUtc >= startOfUtcDay, ct);

        return new WalletSummaryResponse(
            BalanceLkr: wallet.BalanceLkr,
            MaxBalanceCapLkr: MaxBalance,
            MinFundingAmountLkr: MinFunding,
            MaxFundingAmountLkr: MaxFunding,
            SuccessfulFundingCountToday: countToday,
            MaxDailyFundingOperations: MaxDailyFundings,
            RemainingFundingOperationsToday: Math.Max(0, MaxDailyFundings - countToday));
    }

    public async Task<DashboardResponse> GetDashboardAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var customerId = ResolveCustomerId(principal);

        var wallet = await db.Wallets.AsNoTracking()
            .SingleAsync(w => w.CustomerId == customerId, ct);

        var gold = await db.GoldHoldings.AsNoTracking()
            .SingleAsync(h => h.CustomerId == customerId, ct);

        var recent = await db.FinancialTransactions.AsNoTracking()
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(5)
            .Select(t => new TransactionSummary(
                t.TransactionId,
                t.Type.ToString(),
                t.AmountLkr,
                t.GoldQuantityGrams,
                t.PostOperationWalletBalanceLkr,
                t.CreatedAtUtc))
            .ToListAsync(ct);

        return new DashboardResponse(wallet.BalanceLkr, gold.QuantityGrams, recent);
    }

    public async Task<(FundingReceiptResponse Receipt, bool IsReplay)> FundWalletAsync(
        ClaimsPrincipal principal,
        FundWalletRequest request,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var customerId = ResolveCustomerId(principal);

        // 1. Input validation & normalization — before touching the DB so failures are cheap.
        var amountLkr = ValidateAndNormalizeFundingAmount(request.AmountLkr);

        var requestHash = ComputeRequestHash(amountLkr);

        // 2. Begin PostgreSQL transaction.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // 3. Acquire FOR UPDATE lock on wallet row.
        // SELECT * does not include PostgreSQL system columns; xmin must be explicit
        // because EF Core's IsRowVersion() maps to the xmin system column (ODQ-002).
        var wallet = await db.Wallets
            .FromSqlRaw(
                "SELECT id, customer_id, balance_lkr, xmin, created_at_utc, updated_at_utc " +
                "FROM wallets WHERE customer_id = {0} FOR UPDATE",
                customerId)
            .SingleAsync(ct);

        // 4. Resolve idempotency record (inside the lock).
        var existing = await db.IdempotencyRecords
            .SingleOrDefaultAsync(r =>
                r.CustomerId == customerId
                && r.Operation == IdempotencyOperation.SimulateFunding
                && r.IdempotencyKey == idempotencyKey, ct);

        if (existing is not null)
        {
            if (existing.Status == IdempotencyStatus.COMPLETED)
            {
                if (existing.RequestHash != requestHash)
                    throw new WalletFailure("IDEMPOTENCY_CONFLICT", 409,
                        "The idempotency key was previously used for this operation with a different amount.");

                // Exact replay: commit the read-only transaction, return original receipt.
                await tx.CommitAsync(ct);
                var original = await db.FinancialTransactions.AsNoTracking()
                    .SingleAsync(t => t.IdempotencyRecordId == existing.IdempotencyRecordId, ct);
                return (ToReceipt(original), true);
            }

            // IN_PROGRESS means a concurrent request has the lock — already impossible here
            // because we hold FOR UPDATE, but handle defensively.
            throw new WalletFailure("OPERATION_IN_PROGRESS", 409,
                "A request with this idempotency key is already being processed. Please retry after a short delay.");
        }

        // 4d. Insert IN_PROGRESS record inside the transaction.
        var now = DateTime.UtcNow;
        var idempotencyRecord = new IdempotencyRecord(
            customerId, IdempotencyOperation.SimulateFunding, idempotencyKey,
            requestHash, null, IdempotencyStatus.IN_PROGRESS, now, null);
        db.IdempotencyRecords.Add(idempotencyRecord);
        await db.SaveChangesAsync(ct);

        // 5. Business rule checks (inside the lock, after IN_PROGRESS inserted).
        // If any check fails, WalletFailure propagates, tx disposes without commit → rollback.
        var startOfUtcDay = now.Date;
        var countToday = await db.FinancialTransactions.AsNoTracking()
            .CountAsync(t => t.CustomerId == customerId
                          && t.Type == TransactionType.WALLET_FUNDING
                          && t.Status == TransactionStatus.COMPLETED
                          && t.CreatedAtUtc >= startOfUtcDay, ct);

        if (countToday >= MaxDailyFundings)
            throw new WalletFailure("DAILY_FUNDING_LIMIT_EXCEEDED", 422,
                $"You have reached the limit of {MaxDailyFundings} successful funding operations for today (UTC). Please try again tomorrow.");

        if (wallet.BalanceLkr + amountLkr > MaxBalance)
            throw new WalletFailure("WALLET_LIMIT_EXCEEDED", 422,
                $"This funding would cause your wallet balance to exceed the maximum of LKR {MaxBalance:N2}. " +
                $"Current balance: LKR {wallet.BalanceLkr:N2}.");

        // 6. Execute the funding atomically.
        wallet.ApplyFunding(amountLkr, now);
        db.Wallets.Update(wallet);

        // Immutable business transaction record.
        var financialTx = new FinancialTransaction(
            customerId,
            TransactionType.WALLET_FUNDING,
            TransactionStatus.COMPLETED,
            amountLkr,
            null, null, null,
            wallet.BalanceLkr,
            null,
            idempotencyRecord.IdempotencyRecordId,
            now);
        db.FinancialTransactions.Add(financialTx);

        // Resolve or create system LKR funding source ledger account.
        var sysFundingAccount = await db.LedgerAccounts
            .SingleOrDefaultAsync(a => a.AccountNumber == SysFundingAccountNumber, ct);
        if (sysFundingAccount is null)
        {
            sysFundingAccount = new LedgerAccount(
                SysFundingAccountNumber,
                "System Simulated Funding Source",
                LedgerUnit.LKR,
                AccountClassification.EQUITY,
                null,
                now);
            db.LedgerAccounts.Add(sysFundingAccount);
        }

        // Resolve or create customer LKR wallet ledger account.
        var customerAccountNumber = $"2001-LKR-CUST-{customerId}";
        var customerLkrAccount = await db.LedgerAccounts
            .SingleOrDefaultAsync(a => a.AccountNumber == customerAccountNumber, ct);
        if (customerLkrAccount is null)
        {
            customerLkrAccount = new LedgerAccount(
                customerAccountNumber,
                "Customer Simulated Wallet",
                LedgerUnit.LKR,
                AccountClassification.LIABILITY,
                customerId,
                now);
            db.LedgerAccounts.Add(customerLkrAccount);
        }

        // Save wallet + transaction + possibly new ledger accounts together.
        await db.SaveChangesAsync(ct);

        // Ledger posting (LEDGER_MODEL.md §4.1):
        // DEBIT  1001-LKR-SYS-FUNDING   — system funding source disbursed
        // CREDIT 2001-LKR-CUST-{Id}     — customer wallet liability increased
        db.LedgerEntries.Add(new LedgerEntry(
            financialTx.TransactionId,
            sysFundingAccount.AccountId,
            LedgerUnit.LKR,
            EntryDirection.DEBIT,
            amountLkr,
            now));
        db.LedgerEntries.Add(new LedgerEntry(
            financialTx.TransactionId,
            customerLkrAccount.AccountId,
            LedgerUnit.LKR,
            EntryDirection.CREDIT,
            amountLkr,
            now));

        // Mark idempotency COMPLETED with the financial transaction reference.
        MarkIdempotencyCompleted(idempotencyRecord, financialTx.TransactionId, now);
        db.IdempotencyRecords.Update(idempotencyRecord);

        await db.SaveChangesAsync(ct);

        // 7. Commit — all or nothing.
        await tx.CommitAsync(ct);

        return (ToReceipt(financialTx), false);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private static Guid ResolveCustomerId(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (id is null || !Guid.TryParse(id, out var customerId))
            throw new InvalidOperationException("Authenticated customer ID is missing from the session principal.");
        return customerId;
    }

    private static decimal ValidateAndNormalizeFundingAmount(decimal amount)
    {
        // Reject any non-zero digits past 2 decimal places (e.g. 100.001, 100.009).
        if (amount * 100m != decimal.Truncate(amount * 100m))
            throw new WalletFailure("VALIDATION_ERROR", 400,
                "The funding amount cannot have more than 2 decimal places.");

        // Normalize trailing redundant zeroes (e.g. 100.000 -> 100.00).
        var normalized = decimal.Round(amount, 2);

        if (normalized < MinFunding || normalized > MaxFunding)
            throw new WalletFailure("VALIDATION_ERROR", 400,
                $"The funding amount must be between LKR {MinFunding:N2} and LKR {MaxFunding:N2}.");

        return normalized;
    }

    /// <summary>
    /// Canonical SHA-256 hash of the funding request (CONCURRENCY.md §3.2).
    /// Format: WALLET_FUNDING|amountLkr=&lt;value fixed 2dp&gt;
    /// This is explicit, stable, and human-readable in audit logs.
    /// </summary>
    private static string ComputeRequestHash(decimal amount)
    {
        var canonical = $"WALLET_FUNDING|amountLkr={amount:F2}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static FundingReceiptResponse ToReceipt(FinancialTransaction tx) =>
        new(tx.TransactionId, tx.Type.ToString(), tx.Status.ToString(),
            tx.AmountLkr, tx.PostOperationWalletBalanceLkr, tx.CreatedAtUtc);

    /// <summary>
    /// Reflects COMPLETED status into the domain entity via EF Core change tracking,
    /// preserving private setters on the domain entity.
    /// </summary>
    private void MarkIdempotencyCompleted(IdempotencyRecord record, Guid transactionId, DateTime completedAt)
    {
        var entry = db.Entry(record);
        entry.Property("Status").CurrentValue = IdempotencyStatus.COMPLETED;
        entry.Property("ResponseTransactionId").CurrentValue = transactionId;
        entry.Property("CompletedAtUtc").CurrentValue = completedAt;
    }
}
