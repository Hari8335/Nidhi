using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Nidhi.Application.Wallet;

namespace Nidhi.Application.CustomerRecords;

public sealed record HoldingsResponse(decimal QuantityGrams, decimal? IndicativePortfolioValueLkr,
    decimal? ActivePricePerGramLkr, Guid? PriceVersionId, bool IsPriceFresh);

public sealed class TransactionPageRequest
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}

public sealed record TransactionPageResponse(IReadOnlyList<TransactionSummary> Items,
    int Page, int PageSize, int TotalCount, int TotalPages);

public interface ICustomerReadService
{
    Task<HoldingsResponse> GetHoldingsAsync(ClaimsPrincipal principal, CancellationToken ct);
    Task<TransactionPageResponse> GetTransactionsAsync(ClaimsPrincipal principal, TransactionPageRequest page, CancellationToken ct);
    Task<object> GetTransactionAsync(ClaimsPrincipal principal, Guid id, CancellationToken ct);
}
