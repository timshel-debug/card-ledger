namespace CardService.Domain.ValueObjects;

/// <summary>
/// Represents a foreign exchange rate for a specific currency on a specific date as an immutable value object.
/// </summary>
/// <remarks>
/// <para>FxRate encapsulates data from the U.S. Treasury Reporting Rates of Exchange dataset.</para>
/// <para>Includes validation logic for determining if a rate is valid within a specified lookback window (default 6 months).</para>
/// <para>This value object is immutable and uses structural equality based on currency key, record date, and exchange rate.</para>
/// </remarks>
public sealed class FxRate
{
    /// <summary>
    /// Gets the Treasury country-currency descriptor (e.g., "Australia-Dollar", "Austria-Euro").
    /// </summary>
    /// <value>The treasury currency key that uniquely identifies the currency.</value>
    public string CurrencyKey { get; }
    
    /// <summary>
    /// Gets the date on which this exchange rate is effective.
    /// </summary>
    /// <value>The date (without time) when this rate was recorded.</value>
    public DateOnly RecordDate { get; }
    
    /// <summary>
    /// Gets the exchange rate value representing the conversion ratio from USD to the specified currency.
    /// </summary>
    /// <value>A positive decimal representing how many units of the foreign currency equal 1 USD.</value>
    public decimal ExchangeRate { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FxRate"/> class with the specified parameters.
    /// </summary>
    /// <param name="currencyKey">The Treasury currency key (non-empty string).</param>
    /// <param name="recordDate">The date on which the rate is effective.</param>
    /// <param name="exchangeRate">The exchange rate value (must be positive).</param>
    /// <exception cref="ArgumentException">Thrown when currency key is null/empty or exchange rate is not positive.</exception>
    /// <example>
    /// <code>
    /// var rate = new FxRate("Australia-Dollar", new DateOnly(2024, 12, 31), 1.50m);
    /// </code>
    /// </example>
    public FxRate(string currencyKey, DateOnly recordDate, decimal exchangeRate)
    {
        if (string.IsNullOrWhiteSpace(currencyKey))
            throw new ArgumentException("Currency key cannot be empty.", nameof(currencyKey));

        if (exchangeRate <= 0)
            throw new ArgumentException("Exchange rate must be positive.", nameof(exchangeRate));

        CurrencyKey = currencyKey;
        RecordDate = recordDate;
        ExchangeRate = exchangeRate;
    }

    /// <summary>
    /// Determines whether this exchange rate is valid for the specified anchor date within a lookback window.
    /// </summary>
    /// <param name="anchorDate">The reference date to check against (typically a purchase date or current date).</param>
    /// <param name="monthsBack">The number of months to look back from the anchor date. Default is 6 months.</param>
    /// <returns><see langword="true"/> if the rate's record date is within the valid window (earliest date to anchor date inclusive); otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>The valid window is computed as: <c>[anchorDate.AddMonths(-monthsBack), anchorDate]</c></para>
    /// <para>The window boundaries are inclusive on both ends.</para>
    /// <para>Example: for anchorDate = 2024-12-31 and monthsBack = 6, valid window is [2024-06-30, 2024-12-31]</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var rate = new FxRate("Australia-Dollar", new DateOnly(2024, 9, 30), 1.50m);
    /// var isValid = rate.IsValidForDate(new DateOnly(2024, 12, 31), 6); // true (within 6 months)
    /// </code>
    /// </example>
    public bool IsValidForDate(DateOnly anchorDate, int monthsBack = 6)
    {
        var earliestDate = anchorDate.AddMonths(-monthsBack);
        return RecordDate >= earliestDate && RecordDate <= anchorDate;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current exchange rate by comparing currency key, record date, and rate value.
    /// </summary>
    /// <param name="obj">The object to compare with the current exchange rate.</param>
    /// <returns><see langword="true"/> if the specified object is an <see cref="FxRate"/> with the same currency key, record date, and exchange rate; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? obj) =>
        obj is FxRate other &&
        CurrencyKey == other.CurrencyKey &&
        RecordDate == other.RecordDate &&
        ExchangeRate == other.ExchangeRate;

    /// <summary>
    /// Serves as the default hash function for the exchange rate.
    /// </summary>
    /// <returns>A hash code based on the currency key, record date, and exchange rate value.</returns>
    public override int GetHashCode() => HashCode.Combine(CurrencyKey, RecordDate, ExchangeRate);
}
