using System.Linq.Expressions;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nidhi.Application.Admin;
using Nidhi.Application.Wallet;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Persistence;

namespace Nidhi.Infrastructure.Admin;

public sealed class AdminService(NidhiDbContext db) : IAdminService
{
    private static readonly Expression<Func<GoldPrice, PricePublication>> PriceDto = p =>
        new(p.PriceVersionId, p.PricePerGramLkr, p.PublishedAtUtc, p.PublishedByAdminId, p.Reason);
    private static readonly Expression<Func<FinancialTransaction, AdminTransaction>> TransactionDto = t =>
        new(t.TransactionId, t.CustomerId, t.Type.ToString(), t.Status.ToString(), t.AmountLkr,
            t.PriceVersionId, t.AppliedPricePerGramLkr, t.GoldQuantityGrams,
            t.Type == TransactionType.GOLD_PURCHASE ? t.AmountLkr - t.GoldQuantityGrams * t.AppliedPricePerGramLkr : null,
            t.PostOperationWalletBalanceLkr, t.PostOperationGoldHoldingGrams, t.CreatedAtUtc);
    private static WalletFailure Missing(string name) => new("RESOURCE_NOT_FOUND", 404, $"{name} not found.");
    private static async Task<AdminPage<T>> Page<T>(IQueryable<T> rows, AdminPageRequest query, CancellationToken ct)
    {
        var count = await rows.CountAsync(ct);
        var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset >= count ? [] : await rows.Skip((int)offset).Take(query.PageSize).ToListAsync(ct);
        return new(items, query.Page, query.PageSize, count, (int)(((long)count + query.PageSize - 1) / query.PageSize));
    }
    public async Task<AdminDashboard> Dashboard(CancellationToken ct)
    {
        var customers = await db.CustomerProfiles.CountAsync(ct);
        var transactions = await db.FinancialTransactions.CountAsync(ct);
        var price = await db.GoldPrices.AsNoTracking().OrderByDescending(p => p.PublishedAtUtc)
            .ThenByDescending(p => p.PriceVersionId).Select(PriceDto).FirstOrDefaultAsync(ct);
        return new(customers, transactions, price is null ? null : new(price.PriceVersionId, price.PricePerGramLkr,
            price.PublishedAtUtc, DateTime.UtcNow - price.PublishedAtUtc <= TimeSpan.FromHours(24)));
    }
    public Task<AdminPage<CustomerSummary>> Customers(CustomerQuery query, CancellationToken ct)
    {
        var rows = from p in db.CustomerProfiles.AsNoTracking()
                   join u in db.Users.AsNoTracking() on p.CustomerId equals u.Id
                   select new { Id = p.CustomerId, u.Email, p.DisplayName, IsEmailVerified = u.EmailConfirmed, p.CreatedAtUtc };
        if (!string.IsNullOrEmpty(query.Search))
            rows = rows.Where(p => (p.Email != null && p.Email.ToLower().Contains(query.Search.ToLower())) || p.Id.ToString().Contains(query.Search.ToLower()));
        return Page(rows.OrderByDescending(p => p.CreatedAtUtc).ThenByDescending(p => p.Id)
            .Select(p => new CustomerSummary(p.Id, p.Email, p.DisplayName, p.IsEmailVerified, p.CreatedAtUtc)), query, ct);
    }
    public async Task<CustomerDetail> Customer(Guid id, CancellationToken ct)
    {
        var row = await (from p in db.CustomerProfiles.AsNoTracking()
                         join u in db.Users.AsNoTracking() on p.CustomerId equals u.Id
                         where p.CustomerId == id
                         select new CustomerDetail(p.CustomerId, u.Email, p.DisplayName, u.EmailConfirmed, p.CreatedAtUtc,
                             db.Wallets.Where(w => w.CustomerId == id).Select(w => (decimal?)w.BalanceLkr).FirstOrDefault(),
                             db.GoldHoldings.Where(h => h.CustomerId == id).Select(h => (decimal?)h.QuantityGrams).FirstOrDefault()))
            .SingleOrDefaultAsync(ct);
        return row ?? throw Missing("Customer");
    }
    public Task<AdminPage<AdminTransaction>> Transactions(TransactionQuery query, CancellationToken ct)
    {
        var rows = db.FinancialTransactions.AsNoTracking();
        if (query.CustomerId is { } id) rows = rows.Where(t => t.CustomerId == id);
        if (query.Type is not null) { var type = Enum.Parse<TransactionType>(query.Type); rows = rows.Where(t => t.Type == type); }
        return Page(rows.OrderByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.TransactionId).Select(TransactionDto), query, ct);
    }
    public async Task<AdminTransaction> Transaction(Guid id, CancellationToken ct) =>
        await db.FinancialTransactions.AsNoTracking().Where(t => t.TransactionId == id).Select(TransactionDto).SingleOrDefaultAsync(ct)
        ?? throw Missing("Transaction");
    public async Task<TransactionLedger> Ledger(Guid id, CancellationToken ct)
    {
        if (!await db.FinancialTransactions.AnyAsync(t => t.TransactionId == id, ct)) throw Missing("Transaction");
        // One committed operation has two (funding) or four (purchase) postings; the contract is transaction-scoped.
        var rows = await (from e in db.LedgerEntries.AsNoTracking()
                          join a in db.LedgerAccounts.AsNoTracking() on e.AccountId equals a.AccountId
                          where e.TransactionId == id
                          orderby e.CreatedAtUtc, e.EntryId
                          select new LedgerPosting(e.EntryId, a.AccountNumber, a.Name, e.Unit.ToString(), e.Direction.ToString(), e.Amount, e.CreatedAtUtc)).ToListAsync(ct);
        static LedgerBook Book(IEnumerable<LedgerPosting> rows) {
            var entries = rows.ToList();
            return new(entries.Sum(e => e.Direction == "DEBIT" ? e.Amount : -e.Amount) == 0m, entries);
        }
        return new(id, Book(rows.Where(e => e.Unit == "LKR")), Book(rows.Where(e => e.Unit == "GOLD_GRAMS")));
    }
    public Task<AdminPage<PricePublication>> Prices(AdminPageRequest query, CancellationToken ct) =>
        Page(db.GoldPrices.AsNoTracking().OrderByDescending(p => p.PublishedAtUtc).ThenByDescending(p => p.PriceVersionId).Select(PriceDto), query, ct);
    public async Task<PricePublication> Publish(ClaimsPrincipal principal, PublishPriceRequest request, CancellationToken ct)
    {
        var actor = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var now = DateTime.UtcNow;
        var auditId = Guid.CreateVersion7();
        var price = new GoldPrice(request.PricePerGramLkr, now, actor, request.Reason!.Trim(), auditId);
        var audit = new AuditEvent(actor, "ADMIN", "PUBLISH_PRICE", "GoldPrice", price.PriceVersionId.ToString(),
            JsonSerializer.Serialize(new { priceVersionId = price.PriceVersionId, pricePerGramLkr = price.PricePerGramLkr, reason = price.Reason, publishedByAdminId = actor }), now);
        db.Entry(audit).Property(a => a.EventId).CurrentValue = auditId;
        db.GoldPrices.Add(price); db.AuditEvents.Add(audit);
        // EF SaveChanges wraps both inserts in one PostgreSQL transaction.
        await db.SaveChangesAsync(ct);
        return new(price.PriceVersionId, price.PricePerGramLkr, price.PublishedAtUtc, price.PublishedByAdminId, price.Reason);
    }
    public async Task<AdminPage<AdminAudit>> Audit(AuditQuery query, CancellationToken ct)
    {
        var rows = db.AuditEvents.AsNoTracking();
        if (query.ActorId is { } id) rows = rows.Where(a => a.ActorId == id);
        if (query.Action is not null) rows = rows.Where(a => a.Action == query.Action);
        var page = await Page(rows.OrderByDescending(a => a.TimestampUtc).ThenByDescending(a => a.EventId)
            .Select(a => new { a.EventId, a.ActorId, a.ActorRole, a.Action, a.EntityType, a.EntityId, a.DetailsJson, a.TimestampUtc }), query, ct);
        // Allowlist known structured evidence; future/unrecognized event metadata stays hidden.
        static JsonElement Safe(string action, string json) {
            try {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return JsonSerializer.SerializeToElement(new { });
                var keys = action == "PUBLISH_PRICE" ? new[] { "priceVersionId", "pricePerGramLkr", "reason", "publishedByAdminId" }
                    : action == "ADMIN_PROVISIONED" ? new[] { "source" } : [];
                return JsonSerializer.SerializeToElement(doc.RootElement.EnumerateObject().Where(p => keys.Contains(p.Name)).ToDictionary(p => p.Name, p => p.Value.Clone()));
            } catch (JsonException) { return JsonSerializer.SerializeToElement(new { }); }
        }
        return new(page.Items.Select(a => new AdminAudit(a.EventId, a.ActorId, a.ActorRole, a.Action, a.EntityType, a.EntityId, Safe(a.Action, a.DetailsJson), a.TimestampUtc)).ToList(),
            page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }
}
