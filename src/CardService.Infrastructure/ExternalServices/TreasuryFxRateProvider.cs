using System.Text.Json;
using CardService.Application.Ports;
using CardService.Domain.ValueObjects;

namespace CardService.Infrastructure.ExternalServices;

/// <summary>
/// Implementation of the Treasury FX provider port that queries the U.S. Treasury Reporting Rates of Exchange API.
/// </summary>
/// <remarks>
/// <para>
/// This provider integrates with the Treasury Fiscal Data Service API to fetch exchange rates.
/// The integration is designed to be wrapped with Polly resilience policies at the dependency injection
/// layer (timeout, retry, circuit breaker), making this implementation focus solely on request formation
/// and response parsing.
/// </para>
/// <para>
/// HTTP Client Configuration:
/// <list type="bullet">
/// <item>Injected via ASP.NET Core's HttpClientFactory with Polly policies attached</item>
/// <item>Base URL configured from environment variable <c>FX__BaseUrl</c></item>
/// <item>Timeout, retry, and circuit breaker policies configured separately (see DependencyInjection.cs)</item>
/// </list>
/// </para>
/// <para>
/// Treasury API Request Pattern:
/// <list type="bullet">
/// <item>Endpoint: <c>v1/accounting/od/rates_of_exchange</c></item>
/// <item>Filters: currency key, record date range (within 6 months of anchor date)</item>
/// <item>Sort: descending by record_date to retrieve latest rate</item>
/// <item>Page size: 1 (only interested in the most recent rate)</item>
/// </list>
/// </para>
/// </remarks>
public class TreasuryFxRateProvider : ITreasuryFxRateProvider
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="TreasuryFxRateProvider"/> class.
    /// </summary>
    /// <remarks>
    /// The HttpClient should be injected via the HttpClientFactory with Polly policies attached
    /// for resilience (timeout, retry, circuit breaker).
    /// </remarks>
    /// <param name="httpClient">The HTTP client configured with Treasury API base URL and resilience policies.</param>
    public TreasuryFxRateProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Retrieves the latest exchange rate from the Treasury API for the specified currency and date range.
    /// </summary>
    /// <remarks>
    /// <para>
    /// API Request Formation:
    /// <list type="bullet">
    /// <item>Filter by <paramref name="currencyKey"/> (country_currency_desc from Treasury dataset)</item>
    /// <item>Filter by record_date &lt;= <paramref name="anchorDate"/> and &gt;= (anchorDate - <paramref name="monthsBack"/> months)</item>
    /// <item>Sort descending by record_date to get latest applicable rate</item>
    /// <item>Page size 1 for efficiency</item>
    /// </list>
    /// </para>
    /// <para>
    /// Response Parsing:
    /// <list type="bullet">
    /// <item>Parses JSON response from Treasury API</item>
    /// <item>Extracts the first (and only) record from the data array</item>
    /// <item>Parses record_date (ISO date string) and exchange_rate (decimal string)</item>
    /// <item>Returns <c>null</c> if no results or required fields are missing</item>
    /// </list>
    /// </para>
    /// <para>
    /// Resilience: Any exceptions (network failure, timeout, etc.) are allowed to propagate.
    /// The calling <see cref="FxRateResolver"/> handles exceptions and implements cache fallback logic.
    /// </para>
    /// </remarks>
    /// <param name="currencyKey">Treasury country_currency_desc identifier (e.g., "Australia-Dollar").</param>
    /// <param name="anchorDate">The target date defining the upper bound of the search window.</param>
    /// <param name="monthsBack">Number of calendar months to look back; default 6.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The most recent <see cref="FxRate"/> from the Treasury API within the date window,
    /// or <c>null</c> if no matching record is found.</returns>
    /// <exception cref="HttpRequestException">Thrown on network failures or non-2xx HTTP status (after Polly policies).</exception>
    /// <exception cref="JsonException">Thrown on malformed JSON response.</exception>
    /// <example>
    /// <code>
    /// var provider = new TreasuryFxRateProvider(httpClient);
    /// var fxRate = await provider.GetLatestRateAsync("Canada-Dollar", new DateOnly(2024, 11, 15), 6);
    /// // Request URL (URL-encoded):
    /// // v1/accounting/od/rates_of_exchange?filter=record_date:lte:2024-11-15,record_date:gte:2024-05-15,country_currency_desc:eq:Canada-Dollar&sort=-record_date&page[size]=1
    /// </code>
    /// </example>
    public async Task<FxRate?> GetLatestRateAsync(string currencyKey, DateOnly anchorDate, int monthsBack = 6, CancellationToken cancellationToken = default)
    {
        var earliestDate = anchorDate.AddMonths(-monthsBack);

        // Build query parameters
        var filters = $"record_date:lte:{anchorDate:yyyy-MM-dd},record_date:gte:{earliestDate:yyyy-MM-dd},country_currency_desc:eq:{currencyKey}";
        var sort = "-record_date";
        var pageSize = "1";

        var url = $"v1/accounting/od/rates_of_exchange?filter={Uri.EscapeDataString(filters)}&sort={sort}&page[size]={pageSize}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = JsonDocument.Parse(content);

        if (!document.RootElement.TryGetProperty("data", out var dataArray) || dataArray.GetArrayLength() == 0)
            return null;

        var firstRecord = dataArray[0];
        var recordDateStr = firstRecord.GetProperty("record_date").GetString();
        var exchangeRateStr = firstRecord.GetProperty("exchange_rate").GetString();

        if (string.IsNullOrWhiteSpace(recordDateStr) || string.IsNullOrWhiteSpace(exchangeRateStr))
            return null;

        var recordDate = DateOnly.Parse(recordDateStr);
        var exchangeRate = decimal.Parse(exchangeRateStr);

        return new FxRate(currencyKey, recordDate, exchangeRate);
    }
}
