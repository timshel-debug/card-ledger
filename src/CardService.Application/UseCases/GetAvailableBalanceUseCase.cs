using CardService.Application.Common;
using CardService.Application.DTOs;
using CardService.Application.Exceptions;
using CardService.Application.Ports;
using CardService.Application.Services;
using CardService.Domain.ValueObjects;

namespace CardService.Application.UseCases;

/// <summary>
/// Use case for retrieving a card's available balance, optionally converted to a specified currency.
/// </summary>
/// <remarks>
/// <para>
/// This use case provides the ability to query the current available balance for a card.
/// The available balance is calculated as: Credit Limit − Sum(All Purchase Amounts).
/// </para>
/// <para>
/// Key features:
/// <list type="bullet">
/// <item><strong>USD Balance (default):</strong> Returns balance in US dollars without conversion.</item>
/// <item><strong>Converted Balance (optional):</strong> If a <c>currencyKey</c> is provided, converts the USD balance to the target currency.</item>
/// <item><strong>Anchor Date:</strong> For conversion, uses either the provided <c>asOfDate</c> (for deterministic queries) or the current UTC date (for real-time queries).</item>
/// <item><strong>Rate Resolution:</strong> Delegates to <see cref="FxRateResolver"/> for cache-first, resilient rate lookup.</item>
/// </list>
/// </para>
/// <para>
/// The use case aggregates all purchases for the card to compute the total, then applies
/// the exchange rate if conversion is requested.
/// </para>
/// </remarks>
public class GetAvailableBalanceUseCase
{
    private readonly ICardRepository _cardRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly FxRateResolver _fxRateResolver;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAvailableBalanceUseCase"/> class.
    /// </summary>
    /// <param name="cardRepository">Repository for querying card credit limit and identifying the card.</param>
    /// <param name="purchaseRepository">Repository for aggregating purchases to compute total spent.</param>
    /// <param name="fxRateResolver">Service for resolving exchange rates with cache-first strategy and upstream fallback.</param>
    /// <param name="clock">Clock abstraction for determining the default anchor date (current UTC) when conversion is requested without an explicit as-of date.</param>
    public GetAvailableBalanceUseCase(
        ICardRepository cardRepository, 
        IPurchaseRepository purchaseRepository,
        FxRateResolver fxRateResolver, 
        IClock clock)
    {
        _cardRepository = cardRepository;
        _purchaseRepository = purchaseRepository;
        _fxRateResolver = fxRateResolver;
        _clock = clock;
    }

    /// <summary>
    /// Executes the balance retrieval use case, returning the available balance with optional currency conversion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Execution flow:
    /// <list type="number">
    /// <item>Query the card repository to ensure the card exists and retrieve its credit limit.</item>
    /// <item>Aggregate all purchases for the card to compute total amount spent.</item>
    /// <item>Calculate available balance: credit limit − total purchases (minimum 0).</item>
    /// <item>If no <paramref name="currencyKey"/> provided, return USD balance only.</item>
    /// <item>If <paramref name="currencyKey"/> provided:
    /// <list type="bullet">
    /// <item>Determine the anchor date: use <paramref name="asOfDate"/> if provided, otherwise use current UTC date.</item>
    /// <item>Invoke <see cref="FxRateResolver.ResolveRateAsync"/> to find an applicable rate for the anchor date.</item>
    /// <item>Convert the available balance using the resolved rate.</item>
    /// </list>
    /// </item>
    /// <item>Return a <see cref="BalanceResponse"/> with USD balance, conversion details (if requested), and metadata.</item>
    /// </list>
    /// </remarks>
    /// <param name="cardId">The GUID of the card for which to retrieve the balance.</param>
    /// <param name="currencyKey">Optional Treasury country_currency_desc identifier (e.g., "Australia-Dollar"). If <c>null</c> or empty, balance is returned in USD only.</param>
    /// <param name="asOfDate">Optional anchor date for FX rate resolution. If <c>null</c>, defaults to the current UTC date. Used to support deterministic queries (e.g., for testing or historical queries).</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="BalanceResponse"/> containing the credit limit, total purchases, available balance in USD, and (if conversion requested) the converted balance, exchange rate, rate date, and anchor date.</returns>
    /// <exception cref="ResourceNotFoundException">Thrown if no card with the given <paramref name="cardId"/> exists.</exception>
    /// <exception cref="FxConversionUnavailableException">Thrown if <paramref name="currencyKey"/> is specified but no exchange rate is available
    /// on or before the anchor date within the preceding 6 months (HTTP 422).</exception>
    /// <exception cref="FxUpstreamUnavailableException">Thrown if <paramref name="currencyKey"/> is specified, the Treasury API is unavailable,
    /// and no cached rate can be used as a fallback (HTTP 503).</exception>
    /// <example>
    /// <code>
    /// var useCase = new GetAvailableBalanceUseCase(cardRepository, purchaseRepository, fxRateResolver, clock);
    /// 
    /// // Retrieve balance in USD
    /// var usdBalance = await useCase.ExecuteAsync(cardId);
    /// Console.WriteLine($"Available in USD: ${usdBalance.AvailableBalanceUsd:F2}");
    /// 
    /// // Retrieve balance converted to a specific currency as of a specific date
    /// var convertedBalance = await useCase.ExecuteAsync(
    ///     cardId: cardId,
    ///     currencyKey: "Canada-Dollar",
    ///     asOfDate: new DateOnly(2024, 11, 15)
    /// );
    /// Console.WriteLine($"Available in CAD: {convertedBalance.ConvertedAvailableBalance:F2}");
    /// Console.WriteLine($"Exchange Rate: {convertedBalance.ExchangeRate} (as of {convertedBalance.RateDate})");
    /// </code>
    /// </example>
    public async Task<BalanceResponse> ExecuteAsync(
        Guid cardId, 
        string? currencyKey = null, 
        DateOnly? asOfDate = null, 
        CancellationToken cancellationToken = default)
    {
        // Get card
        var card = await _cardRepository.GetByIdAsync(cardId, cancellationToken);
        if (card == null)
            throw new ResourceNotFoundException($"Card with ID {cardId} not found.");

        // Get all purchases
        var purchases = await _purchaseRepository.GetByCardIdAsync(cardId, cancellationToken);
        var totalPurchasesCents = purchases.Sum(p => p.AmountCents);

        var creditLimitMoney = Money.FromCents(card.CreditLimitCents, "USD");
        var totalPurchasesMoney = Money.FromCents(totalPurchasesCents, "USD");
        var availableBalanceCents = Math.Max(0, card.CreditLimitCents - totalPurchasesCents);
        var availableBalanceMoney = Money.FromCents(availableBalanceCents, "USD");

        // If no conversion requested, return USD only
        if (string.IsNullOrWhiteSpace(currencyKey))
        {
            return new BalanceResponse(
                card.Id,
                creditLimitMoney.ToDecimal(),
                totalPurchasesMoney.ToDecimal(),
                availableBalanceMoney.ToDecimal()
            );
        }

        // Conversion requested
        var anchorDate = asOfDate ?? _clock.UtcToday;
        var fxRate = await _fxRateResolver.ResolveRateAsync(currencyKey, anchorDate, cancellationToken);

        var convertedBalance = availableBalanceMoney.ConvertTo(currencyKey, fxRate.ExchangeRate);

        return new BalanceResponse(
            card.Id,
            creditLimitMoney.ToDecimal(),
            totalPurchasesMoney.ToDecimal(),
            availableBalanceMoney.ToDecimal(),
            currencyKey,
            fxRate.ExchangeRate,
            fxRate.RecordDate,
            convertedBalance.ToDecimal(),
            anchorDate
        );
    }
}
