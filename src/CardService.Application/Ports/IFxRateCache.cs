using CardService.Domain.ValueObjects;

namespace CardService.Application.Ports;

/// <summary>
/// Port interface for caching foreign exchange rates retrieved from the Treasury provider.
/// </summary>
/// <remarks>
/// <para>
/// The FX Rate Cache is responsible for storing previously fetched exchange rates to reduce
/// dependency on the external Treasury API and provide resilience against upstream failures.
/// </para>
/// <para>
/// The cache implements a "cache-first" strategy: when resolving an FX rate for currency conversion,
/// the system checks the cache before attempting to fetch from the upstream Treasury provider.
/// </para>
/// <para>
/// The cache stores composite key entries (currency_key + record_date) and supports queries
/// for the latest rate within a specified date window (e.g., last 6 months).
/// </para>
/// </remarks>
public interface IFxRateCache
{
    /// <summary>
    /// Retrieves the latest cached exchange rate for a specified currency within a date window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method performs a date-bound search to find the most recent exchange rate for
    /// <paramref name="currencyKey"/> where the rate's effective date is less than or equal to
    /// <paramref name="anchorDate"/> and falls within the preceding <paramref name="monthsBack"/> months.
    /// </para>
    /// <para>
    /// For example, given an anchor date of 2024-12-31 and monthsBack=6, the search window includes
    /// all rates from 2024-06-30 through 2024-12-31 (calendar month boundaries, inclusive).
    /// </para>
    /// <para>
    /// Returns <c>null</c> if no cached rate exists within the specified window.
    /// </para>
    /// </remarks>
    /// <param name="currencyKey">Treasury country_currency_desc identifier (e.g., "Australia-Dollar", "Austria-Euro").</param>
    /// <param name="anchorDate">The target date used to define the upper bound of the search window.</param>
    /// <param name="monthsBack">Number of calendar months to look back; default 6. Defines the lower bound of the window.</param>
    /// <param name="cancellationToken">Cancellation token to support graceful shutdown or timeout scenarios.</param>
    /// <returns>
    /// The most recent cached <see cref="FxRate"/> within the date window, or <c>null</c> if no matching rate
    /// is found in the cache.
    /// </returns>
    /// <example>
    /// <code>
    /// // Resolve the latest cached rate for Australian Dollar on or before 2024-11-01
    /// var cachedRate = await cache.GetLatestRateAsync("Australia-Dollar", new DateOnly(2024, 11, 01), 6);
    /// if (cachedRate != null)
    /// {
    ///     decimal convertedAmount = usdAmount * cachedRate.ExchangeRate;
    /// }
    /// </code>
    /// </example>
    Task<FxRate?> GetLatestRateAsync(string currencyKey, DateOnly anchorDate, int monthsBack = 6, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores or updates an exchange rate in the cache.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method implements an "upsert" operation: if a rate with the same currencyKey and recordDate
    /// already exists, it is updated; otherwise, a new entry is created.
    /// </para>
    /// <para>
    /// The <see cref="FxRate"/> value object contains the currency key, record date, and exchange rate value.
    /// These values are automatically extracted and persisted.
    /// </para>
    /// </remarks>
    /// <param name="rate">The <see cref="FxRate"/> value object containing the currency key, record date, and exchange rate.</param>
    /// <param name="cancellationToken">Cancellation token to support graceful shutdown or timeout scenarios.</param>
    /// <example>
    /// <code>
    /// var fxRate = new FxRate("Canada-Dollar", new DateOnly(2024, 11, 15), 0.74m);
    /// await cache.UpsertAsync(fxRate);
    /// </code>
    /// </example>
    Task UpsertAsync(FxRate rate, CancellationToken cancellationToken = default);
}
