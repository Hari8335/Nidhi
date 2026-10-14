using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nidhi.Domain.Entities;

namespace Nidhi.Application.Admin;

public class AdminPageRequest
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}
public sealed class CustomerQuery : AdminPageRequest
{
    [StringLength(256)] public string? Search { get; init; }
}
public sealed class TransactionQuery : AdminPageRequest, IValidatableObject
{
    public Guid? CustomerId { get; init; }
    public string? Type { get; init; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Type is not null && Type is not ("WALLET_FUNDING" or "GOLD_PURCHASE"))
            yield return new("Unsupported transaction type.", [nameof(Type)]);
    }
}
public sealed class AuditQuery : AdminPageRequest
{
    public Guid? ActorId { get; init; }
    [StringLength(100)] public string? Action { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishPriceRequest(
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal PricePerGramLkr,
    string? Reason) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (PricePerGramLkr <= 0 || PricePerGramLkr >= 100_000_000_000_000m ||
            (decimal.GetBits(PricePerGramLkr)[3] >> 16 & 255) > 4)
            yield return new("Enter a positive LKR price with at most 14 integer and 4 decimal places.", [nameof(PricePerGramLkr)]);
        if (string.IsNullOrWhiteSpace(Reason) || Reason.Length > 500)
            yield return new("A reason of 1–500 characters is required.", [nameof(Reason)]);
    }
}
public sealed record AdminPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
public sealed record CustomerSummary(Guid Id, string? Email, string? DisplayName, bool IsEmailVerified, DateTime CreatedAtUtc);
public sealed record CustomerDetail(Guid Id, string? Email, string? DisplayName, bool IsEmailVerified,
    DateTime CreatedAtUtc, decimal? WalletBalanceLkr, decimal? GoldHoldingGrams);
public sealed record AdminTransaction(Guid TransactionId, Guid CustomerId, string Type, string Status,
    decimal AmountLkr, Guid? PriceVersionId, decimal? AppliedPricePerGramLkr, decimal? GoldQuantityGrams,
    decimal? ConversionResidualLkr, decimal PostOperationWalletBalanceLkr, decimal? PostOperationGoldHoldingGrams, DateTime CreatedAtUtc);
public sealed record PricePublication(Guid PriceVersionId, decimal PricePerGramLkr, DateTime PublishedAtUtc, Guid PublishedByAdminId, string Reason);
public sealed record ActivePrice(Guid PriceVersionId, decimal PricePerGramLkr, DateTime PublishedAtUtc, bool IsFresh);
public sealed record AdminDashboard(int TotalCustomersCount, int TotalCompletedTransactionsCount, ActivePrice? ActiveGoldPrice);
public sealed record LedgerPosting(Guid EntryId, string AccountNumber, string AccountName, string Unit, string Direction, decimal Amount, DateTime CreatedAtUtc);
public sealed record LedgerBook(bool IsBalanced, IReadOnlyList<LedgerPosting> Entries);
public sealed record TransactionLedger(Guid TransactionId, LedgerBook LkrBook, LedgerBook GoldBook);
public sealed record AdminAudit(Guid Id, Guid ActorId, string ActorRole, string Action, string EntityType, string EntityId, JsonElement Details, DateTime TimestampUtc);
public interface IAdminService
{
    Task<AdminDashboard> Dashboard(CancellationToken ct);
    Task<AdminPage<CustomerSummary>> Customers(CustomerQuery query, CancellationToken ct);
    Task<CustomerDetail> Customer(Guid id, CancellationToken ct);
    Task<AdminPage<AdminTransaction>> Transactions(TransactionQuery query, CancellationToken ct);
    Task<AdminTransaction> Transaction(Guid id, CancellationToken ct);
    Task<TransactionLedger> Ledger(Guid id, CancellationToken ct);
    Task<AdminPage<PricePublication>> Prices(AdminPageRequest query, CancellationToken ct);
    Task<PricePublication> Publish(ClaimsPrincipal principal, PublishPriceRequest request, CancellationToken ct);
    Task<AdminPage<AdminAudit>> Audit(AuditQuery query, CancellationToken ct);
}
