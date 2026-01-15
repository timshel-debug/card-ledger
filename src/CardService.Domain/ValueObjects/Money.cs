namespace CardService.Domain.ValueObjects;

/// <summary>
/// Represents an amount of money in a specific currency as an immutable value object.
/// </summary>
/// <remarks>
/// <para>Money stores amounts as integer cents to avoid floating-point precision errors.</para>
/// <para>Currency conversions use <see cref="decimal"/> arithmetic with <see cref="MidpointRounding.AwayFromZero"/> rounding.</para>
/// <para>All monetary values must be non-negative; zero amounts are permitted.</para>
/// </remarks>
public sealed class Money
{
    /// <summary>
    /// Gets the monetary amount in cents (minor units).
    /// </summary>
    /// <value>The amount in cents as a 64-bit integer.</value>
    /// <remarks>
    /// For USD, 100 cents = 1 dollar. Store as cents to eliminate floating-point errors.
    /// </remarks>
    public long AmountInCents { get; }
    
    /// <summary>
    /// Gets the ISO 4217 currency code or Treasury currency key.
    /// </summary>
    /// <value>The currency code (e.g., "USD", "EUR", "Australia-Dollar").</value>
    public string Currency { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Money"/> class with the specified amount and currency.
    /// </summary>
    /// <param name="amountInCents">The amount in cents.</param>
    /// <param name="currency">The currency code.</param>
    private Money(long amountInCents, string currency)
    {
        AmountInCents = amountInCents;
        Currency = currency;
    }

    /// <summary>
    /// Creates a new <see cref="Money"/> instance from a decimal USD amount.
    /// </summary>
    /// <param name="amount">The amount in US dollars (whole dollars and cents).</param>
    /// <returns>A <see cref="Money"/> instance with the amount converted to cents and currency set to "USD".</returns>
    /// <exception cref="ArgumentException">Thrown when the amount is negative.</exception>
    /// <remarks>
    /// Rounds the decimal amount to the nearest cent using <see cref="MidpointRounding.AwayFromZero"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var money = Money.FromUsd(99.99m);
    /// // money.AmountInCents == 9999
    /// // money.Currency == "USD"
    /// </code>
    /// </example>
    public static Money FromUsd(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        var cents = decimal.Round(amount * 100, 0, MidpointRounding.AwayFromZero);
        return new Money((long)cents, "USD");
    }

    /// <summary>
    /// Creates a new <see cref="Money"/> instance from a cents amount and currency.
    /// </summary>
    /// <param name="cents">The amount in cents (minor units).</param>
    /// <param name="currency">The currency code. Defaults to "USD" if not specified.</param>
    /// <returns>A <see cref="Money"/> instance with the specified amount and currency.</returns>
    /// <exception cref="ArgumentException">Thrown when cents is negative or currency is null/empty.</exception>
    /// <example>
    /// <code>
    /// var money = Money.FromCents(9999, "USD");
    /// // money.ToDecimal() == 99.99m
    /// </code>
    /// </example>
    public static Money FromCents(long cents, string currency = "USD")
    {
        if (cents < 0)
            throw new ArgumentException("Amount in cents cannot be negative.", nameof(cents));

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty.", nameof(currency));

        return new Money(cents, currency);
    }

    /// <summary>
    /// Converts the amount to a decimal representation in the original currency.
    /// </summary>
    /// <returns>The amount as a decimal (e.g., 99.99 for $99.99).</returns>
    /// <remarks>
    /// Returns the value with 2 decimal places using <see cref="MidpointRounding.AwayFromZero"/>.
    /// </remarks>
    public decimal ToDecimal() => decimal.Round(AmountInCents / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Converts the money amount to a target currency using the specified exchange rate.
    /// </summary>
    /// <param name="targetCurrency">The target currency code.</param>
    /// <param name="exchangeRate">The exchange rate from the current currency to the target currency.</param>
    /// <returns>A new <see cref="Money"/> instance with the converted amount and target currency.</returns>
    /// <exception cref="ArgumentException">Thrown when the exchange rate is not positive.</exception>
    /// <remarks>
    /// <para>The conversion formula is: <c>convertedAmount = currentAmount * exchangeRate</c></para>
    /// <para>The result is rounded to the nearest cent using <see cref="MidpointRounding.AwayFromZero"/>.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var usd = Money.FromUsd(100m);
    /// var eur = usd.ConvertTo("EUR", 0.92m); // $100 * 0.92 = €92.00
    /// </code>
    /// </example>
    public Money ConvertTo(string targetCurrency, decimal exchangeRate)
    {
        if (exchangeRate <= 0)
            throw new ArgumentException("Exchange rate must be positive.", nameof(exchangeRate));

        var convertedAmount = ToDecimal() * exchangeRate;
        var convertedCents = decimal.Round(convertedAmount * 100, 0, MidpointRounding.AwayFromZero);
        return new Money((long)convertedCents, targetCurrency);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current money amount by comparing both the amount and currency.
    /// </summary>
    /// <param name="obj">The object to compare with the current money amount.</param>
    /// <returns><see langword="true"/> if the specified object is a <see cref="Money"/> with the same amount in cents and currency; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? obj) => 
        obj is Money other && AmountInCents == other.AmountInCents && Currency == other.Currency;
    
    /// <summary>
    /// Serves as the default hash function for the money amount.
    /// </summary>
    /// <returns>A hash code based on both the amount in cents and the currency.</returns>
    public override int GetHashCode() => HashCode.Combine(AmountInCents, Currency);
    
    /// <summary>
    /// Returns a formatted string representation of the money amount.
    /// </summary>
    /// <returns>A string in the format "99.99 USD" or "100.00 EUR", etc.</returns>
    /// <example>
    /// <code>
    /// var money = Money.FromUsd(99.99m);
    /// Console.WriteLine(money); // "99.99 USD"
    /// </code>
    /// </example>
    public override string ToString() => $"{ToDecimal():F2} {Currency}";
}
