using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Nidhi.Application.CustomerRecords;
using Nidhi.Application.Wallet;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Persistence;

namespace Nidhi.Infrastructure.CustomerRecords;

public sealed class CustomerReadService(NidhiDbContext db) : ICustomerReadService
{
    // Shared by full history and dashboard; projection executes in PostgreSQL.
    internal static readonly Expression<Func<FinancialTransaction, TransactionSummary>> Summary = t =>
        new TransactionSummary(t.TransactionId, t.Type.ToString(), t.AmountLkr, t.GoldQuantityGrams,
            t.AppliedPricePerGramLkr, t.Status.ToString(), t.PostOperationWalletBalanceLkr, t.CreatedAtUtc);

    internal static IOrderedQueryable<FinancialTransaction> History(NidhiDbContext db, Guid customerId) =>
        db.FinancialTransactions.AsNoTracking().Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.TransactionId);

    private static Guid CustomerId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Missing authenticated customer identity."));

    public async Task<HoldingsResponse> GetHoldingsAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var id = CustomerId(principal);
        var quantity = await db.GoldHoldings.AsNoTracking().Where(h => h.CustomerId == id)
            .Select(h => h.QuantityGrams).SingleAsync(ct);
        var price = await db.GoldPrices.AsNoTracking().OrderByDescending(p => p.PublishedAtUtc)
            .FirstOrDefaultAsync(ct);
        var fresh = price is not null && DateTime.UtcNow - price.PublishedAtUtc <= TimeSpan.FromHours(24);
        // Display-only rounding; neither holding nor immutable receipts are changed.
        return new(quantity, fresh ? decimal.Round(quantity * price!.PricePerGramLkr, 2, MidpointRounding.ToEven) : null,
            price?.PricePerGramLkr, price?.PriceVersionId, fresh);
    }

    public async Task<TransactionPageResponse> GetTransactionsAsync(ClaimsPrincipal principal, TransactionPageRequest page, CancellationToken ct)
    {
        var history = History(db, CustomerId(principal));
        var total = await history.CountAsync(ct);
        var offset = ((long)page.Page - 1) * page.PageSize;
        var items = offset >= total ? [] : await history.Skip((int)offset).Take(page.PageSize).Select(Summary).ToListAsync(ct);
        return new(items, page.Page, page.PageSize, total, (int)(((long)total + page.PageSize - 1) / page.PageSize));
    }

    public async Task<object> GetTransactionAsync(ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        var customerId = CustomerId(principal);
        var t = await db.FinancialTransactions.AsNoTracking()
            .SingleOrDefaultAsync(t => t.CustomerId == customerId && t.TransactionId == id, ct);
        if (t is null) throw new WalletFailure("RESOURCE_NOT_FOUND", 404, "Transaction not found.");
        if (t.Type == TransactionType.WALLET_FUNDING)
            return new FundingReceiptResponse(t.TransactionId, t.Type.ToString(), t.Status.ToString(),
                t.AmountLkr, t.PostOperationWalletBalanceLkr, t.CreatedAtUtc);
        return new GoldPurchaseReceiptResponse(t.TransactionId, t.Type.ToString(), t.Status.ToString(), t.AmountLkr,
            t.PriceVersionId!.Value, t.AppliedPricePerGramLkr!.Value, t.GoldQuantityGrams!.Value,
            t.AmountLkr - t.GoldQuantityGrams.Value * t.AppliedPricePerGramLkr.Value,
            t.PostOperationWalletBalanceLkr, t.PostOperationGoldHoldingGrams!.Value, t.CreatedAtUtc);
    }
}
