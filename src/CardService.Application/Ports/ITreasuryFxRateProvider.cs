using CardService.Domain.ValueObjects;

namespace CardService.Application.Ports;

/// <summary>
/// Port interface for retrieving exchange rates from the U.S. Treasury Reporting Rates of Exchange API.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts the integration with the external U.S. Treasury Fiscal Data Service
/// (https://fiscaldata.treasury.gov/datasets/treasury-reporting-rates-exchange/), enabling testability
/// and resilience through dependency injection.
/// </para>
/// <para>
/// Implementations are responsible for:
/// <list type="bullet">
/// <item>Constructing API requests with appropriate filters (currency key, date window)</item>
/// <item>Handling HTTP communication with timeouts and error scenarios</item>
/// <item>Parsing Treasury API responses and extracting the latest applicable rate</item>
/// <item>Translating upstream failures into application exceptions (see <see cref="FxUpstreamUnavailableException"/>)</item>
/// </list>
/// </para>
/// <para>
/// The Treasury API is called with resilience policies (retry, timeout, circuit breaker) managed by Polly.
/// Implementations do not directly apply these policies; instead, they are configured at the dependency
/// injection container level via Polly-decorated HTTP clients.
/// </para>
/// </remarks>
public interface ITreasuryFxRateProvider
{
    /// <summary>
    /// Retrieves the latest exchange rate for a specified currency within a date window from the Treasury API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method queries the U.S. Treasury Reporting Rates of Exchange API for the most recent exchange rate
    /// where the rate's effective date is less than or equal to <paramref name="anchorDate"/> and falls
    /// within the preceding <paramref name="monthsBack"/> calendar months.
    /// </para>
    /// <para>
    /// The API request includes:
    /// <list type="bullet">
    /// <item>Filter by <paramref name="currencyKey"/> (Treasury country_currency_desc)</item>
    /// <item>Filter by record_date &lt;= <paramref name="anchorDate"/></item>
    /// <item>Filter by record_date within preceding <paramref name="monthsBack"/> months</item>
    /// <item>Sort descending by record_date to retrieve the latest applicable rate</item>
    /// <item>Limit results to 1 for efficiency</item>
    /// </list>
    /// </para>
    /// <para>
    /// Returns <c>null</c> if the Treasury API returns no results within the specified window, or
    /// throws <see cref="FxUpstreamUnavailableException"/> if the API is unreachable or unresponsive
    /// (after resilience policy retry/timeout exhaustion).
    /// </para>
    /// </remarks>
    /// <param name="currencyKey">Treasury country_currency_desc identifier (e.g., "Australia-Dollar", "Austria-Euro").</param>
    /// <param name="anchorDate">The target date used to define the upper bound of the search window.</param>
    /// <param name="monthsBack">Number of calendar months to look back; default 6. Defines the lower bound of the window.</param>
    /// <param name="cancellationToken">Cancellation token to support graceful shutdown or timeout scenarios.</param>
    /// <returns>
    /// The most recent <see cref="FxRate"/> from the Treasury API within the date window, or <c>null</c> if
    /// no matching rate is found. Throws <see cref="FxUpstreamUnavailableException"/> on persistent upstream failure.
    /// </returns>
    /// <exception cref="FxUpstreamUnavailableException">Thrown when the Treasury API is unreachable or unresponsive
    /// after resilience policies (timeout, retry) have been exhausted.</exception>
    /// <exception cref="ValidationException">Thrown if <paramref name="currencyKey"/> is null, empty, or whitespace.</exception>
    /// <example>
    /// <code>
    /// // Retrieve the latest FX rate for Canadian Dollar on or before 2024-11-15
    /// try
    /// {
    ///     var fxRate = await treasuryProvider.GetLatestRateAsync("Canada-Dollar", new DateOnly(2024, 11, 15), 6);
    ///     if (fxRate != null)
    ///     {
    ///         decimal convertedAmount = usdAmount * fxRate.ExchangeRate;
    ///     }
    ///     else
    ///     {
    ///         // No rate found within the 6-month window
    ///     }
    /// }
    /// catch (FxUpstreamUnavailableException ex)
    /// {
    ///     // Handle upstream API failure (network, timeout, etc.)
    ///     // This exception is only thrown after resilience policies have been exhausted
    /// }
    /// </code>
    /// </example>
    Task<FxRate?> GetLatestRateAsync(string currencyKey, DateOnly anchorDate, int monthsBack = 6, CancellationToken cancellationToken = default);
}
