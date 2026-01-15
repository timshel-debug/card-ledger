using CardService.Application.Common;
using CardService.Application.Ports;
using CardService.Domain.Entities;
using CardService.Domain.ValueObjects;
using CardService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CardService.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of the FX rate cache port.
/// </summary>
/// <remarks>
/// <para>
/// This repository provides persistent caching of exchange rates fetched from the Treasury API,
/// implementing the <see cref="IFxRateCache"/> port interface. The cache reduces dependency on
/// the external Treasury API and provides resilience for currency conversion requests.
/// </para>
/// <para>
/// Key responsibilities:
/// <list type="bullet">
/// <item>Query the latest cached rate for a currency within a date window</item>
/// <item>Store newly fetched rates for future use (upsert semantics)</item>
/// <item>Enforce date-window constraints (6 months prior to anchor date)</item>
/// </list>
/// </para>
/// <para>
/// The cache uses SQLite's FxRateCache table with composite primary key (currency_key, record_date)
/// to ensure each currency-date combination is stored only once.
/// </para>
/// </remarks>
public class FxRateCacheRepository : IFxRateCache
{
    private readonly AppDbContext _context;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="FxRateCacheRepository"/> class.
    /// </summary>
    /// <param name="context">The EF Core DbContext for database access.</param>
    /// <param name="clock">Clock abstraction for recording cache entry timestamps.</param>
    public FxRateCacheRepository(AppDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>
    /// Retrieves the latest cached exchange rate for a currency within the specified date window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method queries the cache for rates matching:
    /// <list type="bullet">
    /// <item>Currency key exact match</item>
    /// <item>Record date within the window: [anchorDate - monthsBack, anchorDate] (calendar month boundaries)</item>
    /// </list>
    /// </para>
    /// <para>
    /// Returns the single rate with the latest record_date to comply with the business requirement
    /// "latest rate ≤ anchor date within 6 months".
    /// </para>
    /// </remarks>
    /// <param name="currencyKey">Treasury country_currency_desc identifier (e.g., "Australia-Dollar").</param>
    /// <param name="anchorDate">The target date defining the upper bound of the search window.</param>
    /// <param name="monthsBack">Number of calendar months to look back; default 6.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The most recent cached <see cref="FxRate"/> within the window, or <c>null</c> if no match found.</returns>
    public async Task<FxRate?> GetLatestRateAsync(string currencyKey, DateOnly anchorDate, int monthsBack = 6, CancellationToken cancellationToken = default)
    {
        var earliestDate = anchorDate.AddMonths(-monthsBack);

        var entry = await _context.FxRateCache
            .Where(e => e.CurrencyKey == currencyKey)
            .Where(e => e.RecordDate >= earliestDate && e.RecordDate <= anchorDate)
            .OrderByDescending(e => e.RecordDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (entry == null)
            return null;

        return new FxRate(entry.CurrencyKey, entry.RecordDate, entry.ExchangeRate);
    }

    /// <summary>
    /// Stores or updates an exchange rate in the cache.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method implements upsert semantics:
    /// <list type="bullet">
    /// <item>If a rate for the same (currency_key, record_date) exists, it is deleted and replaced.</item>
    /// <item>The new entry is created with the current UTC timestamp (cached_utc).</item>
    /// </list>
    /// </para>
    /// <para>
    /// The delete-then-insert pattern is used because EF Core cannot easily update composite primary key values.
    /// </para>
    /// </remarks>
    /// <param name="rate">The <see cref="FxRate"/> value object to cache (contains currency key, record date, and rate).</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    public async Task UpsertAsync(FxRate rate, CancellationToken cancellationToken = default)
    {
        var existing = await _context.FxRateCache
            .FirstOrDefaultAsync(e => e.CurrencyKey == rate.CurrencyKey && e.RecordDate == rate.RecordDate, cancellationToken);

        if (existing != null)
        {
            // Update - but EF doesn't allow updating key properties easily, so we'll delete and insert
            _context.FxRateCache.Remove(existing);
        }

        var entry = new FxRateCacheEntry(rate.CurrencyKey, rate.RecordDate, rate.ExchangeRate, _clock.UtcNow);
        await _context.FxRateCache.AddAsync(entry, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
