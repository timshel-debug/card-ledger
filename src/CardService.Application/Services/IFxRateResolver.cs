using CardService.Domain.ValueObjects;

namespace CardService.Application.Services;

/// <summary>
/// Port (interface) for resolving foreign exchange rates with cache-first strategy and resilience.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts the FX rate resolution logic, allowing use cases to depend on
/// a contract rather than a concrete implementation (Dependency Inversion Principle).
/// </para>
/// <para>
/// The resolver implements a cache-first strategy with upstream fallback and proper error handling.
/// </para>
/// </remarks>
public interface IFxRateResolver
{
    /// <summary>
    /// Resolves an exchange rate for the specified currency and anchor date.
    /// </summary>
    /// <param name="currencyKey">Treasury country_currency_desc identifier (e.g., "Australia-Dollar", "Austria-Euro").</param>
    /// <param name="anchorDate">The target date used to define the upper bound of the 6-month search window.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="FxRate"/> value object containing the currency key, record date, and exchange rate.</returns>
    /// <exception cref="Exceptions.ValidationException">Thrown if <paramref name="currencyKey"/> is null, empty, or whitespace.</exception>
    /// <exception cref="Exceptions.FxConversionUnavailableException">Thrown if no exchange rate is available for the currency
    /// on or before <paramref name="anchorDate"/> within the preceding 6 months (HTTP 422).</exception>
    /// <exception cref="Exceptions.FxUpstreamUnavailableException">Thrown if the Treasury API is unavailable after retry/timeout exhaustion
    /// and no cached fallback is available (HTTP 503).</exception>
    Task<FxRate> ResolveRateAsync(string currencyKey, DateOnly anchorDate, CancellationToken cancellationToken = default);
}
