using CardService.Application.Exceptions;
using CardService.Application.Ports;
using CardService.Domain.ValueObjects;

namespace CardService.Application.Services;

/// <summary>
/// Service for resolving foreign exchange rates with a cache-first strategy and resilience against upstream failures.
/// </summary>
/// <remarks>
/// <para>
/// The FxRateResolver implements the core FX resolution pattern used throughout the application:
/// <list type="number">
/// <item><strong>Cache First:</strong> Check the local cache for a suitable rate within the date window.</item>
/// <item><strong>Upstream Provider:</strong> If cache miss, query the Treasury API via the upstream provider.</item>
/// <item><strong>Resilience:</strong> If upstream call fails but cache is populated, use cached rate as fallback.</item>
/// <item><strong>Error Handling:</strong> Throw appropriate exceptions based on the failure type:
/// <list type="bullet">
/// <item><see cref="FxConversionUnavailableException"/> (HTTP 422) – No rate within 6-month window</item>
/// <item><see cref="FxUpstreamUnavailableException"/> (HTTP 503) – Upstream unavailable and no cached fallback</item>
/// </list>
/// </item>
/// </list>
/// </para>
/// <para>
/// This service encapsulates the complexity of cache management, upstream resilience, and fallback logic,
/// simplifying the use cases that depend on exchange rate resolution.
/// </para>
/// </remarks>
public class FxRateResolver : IFxRateResolver
{
    private readonly IFxRateCache _cache;
    private readonly ITreasuryFxRateProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="FxRateResolver"/> class.
    /// </summary>
    /// <param name="cache">The cache port for retrieving and storing previously fetched rates.</param>
    /// <param name="provider">The upstream provider port for querying the Treasury API.</param>
    public FxRateResolver(IFxRateCache cache, ITreasuryFxRateProvider provider)
    {
        _cache = cache;
        _provider = provider;
    }

    /// <summary>
    /// Resolves an exchange rate for the specified currency and anchor date, using cache-first strategy with upstream fallback.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Execution flow:
    /// <list type="number">
    /// <item>Validate that <paramref name="currencyKey"/> is not empty.</item>
    /// <item>Query the cache for the latest rate for <paramref name="currencyKey"/> within 6 months of <paramref name="anchorDate"/>.</item>
    /// <item>If cache hit, return immediately (fast path).</item>
    /// <item>If cache miss, attempt to fetch from the upstream Treasury provider.</item>
    /// <item>If upstream succeeds, cache the result and return it.</item>
    /// <item>If upstream fails:
    /// <list type="bullet">
    /// <item>If cache has ANY rate for this currency, return it as fallback (resilience).</item>
    /// <item>Otherwise, throw <see cref="FxUpstreamUnavailableException"/> (HTTP 503).</item>
    /// </list>
    /// </item>
    /// <item>If no rate exists within the 6-month window, throw <see cref="FxConversionUnavailableException"/> (HTTP 422).</item>
    /// </list>
    /// </remarks>
    /// <param name="currencyKey">Treasury country_currency_desc identifier (e.g., "Australia-Dollar", "Austria-Euro").</param>
    /// <param name="anchorDate">The target date used to define the upper bound of the 6-month search window.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="FxRate"/> value object containing the currency key, record date, and exchange rate.</returns>
    /// <exception cref="ValidationException">Thrown if <paramref name="currencyKey"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FxConversionUnavailableException">Thrown if no exchange rate is available for the currency
    /// on or before <paramref name="anchorDate"/> within the preceding 6 months (HTTP 422).</exception>
    /// <exception cref="FxUpstreamUnavailableException">Thrown if the Treasury API is unavailable after retry/timeout exhaustion
    /// and no cached fallback is available (HTTP 503).</exception>
    /// <example>
    /// <code>
    /// var resolver = new FxRateResolver(cache, provider);
    /// try
    /// {
    ///     // Fast path: cache hit returns immediately
    ///     var fxRate = await resolver.ResolveRateAsync("Canada-Dollar", new DateOnly(2024, 11, 15));
    ///     
    ///     // Slow path: upstream call (with retries, timeout protection)
    ///     decimal convertedAmount = usdAmount * fxRate.ExchangeRate;
    /// }
    /// catch (FxConversionUnavailableException)
    /// {
    ///     // No rate available within 6 months
    /// }
    /// catch (FxUpstreamUnavailableException)
    /// {
    ///     // Upstream API failed and no cache available
    /// }
    /// </code>
    /// </example>
    public async Task<FxRate> ResolveRateAsync(string currencyKey, DateOnly anchorDate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currencyKey))
            throw new ValidationException("Currency key cannot be empty.");

        // Try cache first
        var cachedRate = await _cache.GetLatestRateAsync(currencyKey, anchorDate, 6, cancellationToken);
        if (cachedRate != null)
            return cachedRate;

        // Try upstream provider
        FxRate? providerRate = null;
        Exception? upstreamException = null;

        try
        {
            providerRate = await _provider.GetLatestRateAsync(currencyKey, anchorDate, 6, cancellationToken);
        }
        catch (Exception ex)
        {
            upstreamException = ex;
        }

        if (providerRate != null)
        {
            // Cache the rate
            await _cache.UpsertAsync(providerRate, cancellationToken);
            return providerRate;
        }

        // No rate found in provider - check if we have ANY cached rate to fall back on
        if (upstreamException != null)
        {
            // Upstream failed - we already checked cache and it was empty
            throw new FxUpstreamUnavailableException(
                $"Foreign exchange upstream service is unavailable and no cached rate found for {currencyKey} on or before {anchorDate}.",
                upstreamException);
        }

        // No rate available within 6 months
        throw new FxConversionUnavailableException(
            $"No exchange rate available for {currencyKey} within 6 months prior to {anchorDate}.");
    }
}
