using CardService.Application.DTOs;
using CardService.Application.Exceptions;
using CardService.Application.Ports;
using CardService.Application.Services;

namespace CardService.Application.UseCases;

/// <summary>
/// Use case for retrieving a purchase transaction converted to a specified currency.
/// </summary>
/// <remarks>
/// <para>
/// This use case demonstrates the core currency conversion feature of the Card Service:
/// <list type="bullet">
/// <item>Querying a purchase by its card ID and purchase ID</item>
/// <item>Resolving an appropriate exchange rate from the Treasury data (cache-first strategy)</item>
/// <item>Converting the USD purchase amount to the requested currency</item>
/// <item>Returning comprehensive conversion details including the rate used and its effective date</item>
/// </list>
/// </para>
/// <para>
/// The conversion logic ensures:
/// <list type="bullet">
/// <item>The rate's effective date is less than or equal to the purchase transaction date</item>
/// <item>The rate falls within the preceding 6 calendar months from the purchase date</item>
/// <item>If no suitable rate exists, <see cref="FxConversionUnavailableException"/> is thrown (HTTP 422)</item>
/// <item>If the Treasury API is unavailable and no cached fallback exists, <see cref="FxUpstreamUnavailableException"/> is thrown (HTTP 503)</item>
/// </list>
/// </para>
/// </remarks>
public class GetPurchaseConvertedUseCase
{
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IFxRateResolver _fxRateResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetPurchaseConvertedUseCase"/> class.
    /// </summary>
    /// <param name="purchaseRepository">Repository for querying purchase transactions.</param>
    /// <param name="fxRateResolver">Service for resolving exchange rates with cache-first strategy and upstream fallback.</param>
    public GetPurchaseConvertedUseCase(IPurchaseRepository purchaseRepository, IFxRateResolver fxRateResolver)
    {
        _purchaseRepository = purchaseRepository;
        _fxRateResolver = fxRateResolver;
    }

    /// <summary>
    /// Executes the purchase retrieval use case, converting the amount to the specified currency.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Execution flow:
    /// <list type="number">
    /// <item>Validate the currency key is not empty or whitespace.</item>
    /// <item>Query the purchase repository to ensure the purchase belongs to the specified card.</item>
    /// <item>Invoke <see cref="FxRateResolver.ResolveRateAsync"/> to find an applicable exchange rate.</item>
    /// <item>Convert the purchase amount using <see cref="Domain.ValueObjects.Money.ConvertTo"/>.</item>
    /// <item>Return a <see cref="ConvertedPurchaseResponse"/> with original amount, exchange rate, and converted amount.</item>
    /// </list>
    /// </para>
    /// <para>
    /// The conversion applies <see cref="System.MidpointRounding.AwayFromZero"/> rounding to 2 decimal places
    /// (cents) to ensure accurate currency representation.
    /// </para>
    /// </remarks>
    /// <param name="cardId">The GUID of the card that owns the purchase.</param>
    /// <param name="purchaseId">The GUID of the purchase to retrieve and convert.</param>
    /// <param name="currencyKey">Treasury country_currency_desc identifier (e.g., "Australia-Dollar", "Austria-Euro").</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="ConvertedPurchaseResponse"/> containing the purchase details, original USD amount, exchange rate, rate date, and converted amount.</returns>
    /// <exception cref="ValidationException">Thrown if <paramref name="currencyKey"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ResourceNotFoundException">Thrown if no purchase with the given <paramref name="purchaseId"/> exists
    /// for the card with <paramref name="cardId"/>.</exception>
    /// <exception cref="FxConversionUnavailableException">Thrown if no exchange rate is available for the currency key
    /// on or before the purchase date within the preceding 6 months (HTTP 422).</exception>
    /// <exception cref="FxUpstreamUnavailableException">Thrown if the Treasury API is unavailable and no cached rate
    /// can be used as a fallback (HTTP 503).</exception>
    /// <example>
    /// <code>
    /// var useCase = new GetPurchaseConvertedUseCase(purchaseRepository, fxRateResolver);
    /// try
    /// {
    ///     var response = await useCase.ExecuteAsync(
    ///         cardId: new Guid("..."), 
    ///         purchaseId: new Guid("..."), 
    ///         currencyKey: "Canada-Dollar"
    ///     );
    ///     Console.WriteLine($"Original: ${response.AmountUsd:F2} USD");
    ///     Console.WriteLine($"Exchange Rate: {response.ExchangeRate}");
    ///     Console.WriteLine($"Converted: {response.ConvertedAmount:F2} {response.CurrencyKey}");
    /// }
    /// catch (FxConversionUnavailableException)
    /// {
    ///     Console.WriteLine("No exchange rate available within 6 months of purchase date.");
    /// }
    /// </code>
    /// </example>
    public async Task<ConvertedPurchaseResponse> ExecuteAsync(Guid cardId, Guid purchaseId, string currencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currencyKey))
            throw new ValidationException("Currency key cannot be empty.");

        // Get purchase
        var purchase = await _purchaseRepository.GetByCardAndPurchaseIdAsync(cardId, purchaseId, cancellationToken);
        if (purchase == null)
            throw new ResourceNotFoundException($"Purchase with ID {purchaseId} not found for card {cardId}.");

        // Get FX rate
        var fxRate = await _fxRateResolver.ResolveRateAsync(currencyKey, purchase.TransactionDate, cancellationToken);

        // Convert amount
        var purchaseAmount = purchase.GetAmount();
        var convertedMoney = purchaseAmount.ConvertTo(currencyKey, fxRate.ExchangeRate);

        return new ConvertedPurchaseResponse(
            purchase.Id,
            purchase.Description,
            purchase.TransactionDate,
            purchaseAmount.ToDecimal(),
            currencyKey,
            fxRate.ExchangeRate,
            fxRate.RecordDate,
            convertedMoney.ToDecimal()
        );
    }
}
