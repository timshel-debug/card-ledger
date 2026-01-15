namespace CardService.Domain.Entities;

/// <summary>
/// Represents a cached foreign exchange rate entry persisted in the database.
/// </summary>
/// <remarks>
/// <para>FxRateCacheEntry is used internally by the infrastructure layer to cache Treasury FX rates.</para>
/// <para>These cached entries support the FX resolution strategy: cache-first, then upstream provider, then fallback to stale cache.</para>
/// <para>Unlike <see cref="ValueObjects.FxRate"/>, this entity includes a cache timestamp for potential TTL-based invalidation.</para>
/// </remarks>
public sealed class FxRateCacheEntry
{
    /// <summary>
    /// Gets the Treasury country-currency descriptor (e.g., "Australia-Dollar", "Austria-Euro").
    /// </summary>
    /// <value>The treasury currency key that uniquely identifies the currency.</value>
    public string CurrencyKey { get; private set; } = string.Empty;
    
    /// <summary>
    /// Gets the date on which this exchange rate is effective.
    /// </summary>
    /// <value>The date (without time) when this rate was recorded by the Treasury.</value>
    public DateOnly RecordDate { get; private set; }
    
    /// <summary>
    /// Gets the exchange rate value.
    /// </summary>
    /// <value>A positive decimal representing the conversion ratio from USD to the specified currency.</value>
    public decimal ExchangeRate { get; private set; }
    
    /// <summary>
    /// Gets the UTC date and time when this rate was cached.
    /// </summary>
    /// <value>A <see cref="DateTime"/> in UTC indicating when the cache entry was created or updated.</value>
    public DateTime CachedUtc { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FxRateCacheEntry"/> class for Entity Framework Core.
    /// </summary>
    /// <remarks>This parameterless constructor is required by EF Core for materializing entities from the database.</remarks>
    private FxRateCacheEntry() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FxRateCacheEntry"/> class with the specified parameters.
    /// </summary>
    /// <param name="currencyKey">The Treasury currency key (non-empty string).</param>
    /// <param name="recordDate">The date on which the rate is effective.</param>
    /// <param name="exchangeRate">The exchange rate value (must be positive).</param>
    /// <param name="cachedUtc">The UTC timestamp when this entry was cached.</param>
    /// <exception cref="ArgumentException">Thrown when currency key is null/empty or exchange rate is not positive.</exception>
    /// <remarks>
    /// <para>This constructor validates the invariants but does not check for existing duplicates; that is the responsibility of the repository.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var cacheEntry = new FxRateCacheEntry(
    ///     currencyKey: "Australia-Dollar",
    ///     recordDate: new DateOnly(2024, 12, 31),
    ///     exchangeRate: 1.50m,
    ///     cachedUtc: DateTime.UtcNow
    /// );
    /// </code>
    /// </example>
    public FxRateCacheEntry(string currencyKey, DateOnly recordDate, decimal exchangeRate, DateTime cachedUtc)
    {
        if (string.IsNullOrWhiteSpace(currencyKey))
            throw new ArgumentException("Currency key cannot be empty.", nameof(currencyKey));

        if (exchangeRate <= 0)
            throw new ArgumentException("Exchange rate must be positive.", nameof(exchangeRate));

        CurrencyKey = currencyKey;
        RecordDate = recordDate;
        ExchangeRate = exchangeRate;
        CachedUtc = cachedUtc;
    }
}
