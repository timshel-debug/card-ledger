namespace CardService.Domain.ValueObjects;

/// <summary>
/// Represents a 16-digit credit or debit card number as a value object.
/// </summary>
/// <remarks>
/// <para>CardNumber enforces the domain invariant that a card number must be exactly 16 numeric digits.</para>
/// <para>This value object is immutable and uses structural equality based on the numeric value.</para>
/// </remarks>
public sealed class CardNumber
{
    /// <summary>
    /// Gets the normalized 16-digit card number.
    /// </summary>
    /// <value>The full 16-digit card number as a string.</value>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CardNumber"/> class with a validated card number.
    /// </summary>
    /// <param name="value">The 16-digit card number to store.</param>
    private CardNumber(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates and validates a new <see cref="CardNumber"/> from a string.
    /// </summary>
    /// <param name="value">The card number string to validate and create. Must be 16 digits.</param>
    /// <returns>A valid <see cref="CardNumber"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is null, empty, not 16 digits, or contains non-digit characters.</exception>
    /// <example>
    /// <code>
    /// var cardNumber = CardNumber.Create("4111111111111111");
    /// </code>
    /// </example>
    public static CardNumber Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Card number cannot be empty.", nameof(value));

        var normalized = value.Trim();

        if (normalized.Length != 16)
            throw new ArgumentException("Card number must be exactly 16 digits.", nameof(value));

        if (!normalized.All(char.IsDigit))
            throw new ArgumentException("Card number must contain only digits.", nameof(value));

        return new CardNumber(normalized);
    }

    /// <summary>
    /// Gets the last 4 digits of the card number for display purposes.
    /// </summary>
    /// <returns>The last 4 digits of the card number.</returns>
    /// <example>
    /// <code>
    /// var last4 = cardNumber.GetLast4(); // "1111" for "4111111111111111"
    /// </code>
    /// </example>
    public string GetLast4() => Value.Substring(12, 4);

    /// <summary>
    /// Determines whether the specified object is equal to the current card number by comparing their numeric values.
    /// </summary>
    /// <param name="obj">The object to compare with the current card number.</param>
    /// <returns><see langword="true"/> if the specified object is a <see cref="CardNumber"/> with the same numeric value; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? obj) => obj is CardNumber other && Value == other.Value;
    
    /// <summary>
    /// Serves as the default hash function for the card number.
    /// </summary>
    /// <returns>A hash code for the current card number's numeric value.</returns>
    public override int GetHashCode() => Value.GetHashCode();
    
    /// <summary>
    /// Returns a masked string representation of the card number for safe display.
    /// </summary>
    /// <returns>A string in the format "****-****-****-XXXX" where XXXX is the last 4 digits.</returns>
    /// <example>
    /// <code>
    /// var display = cardNumber.ToString(); // "****-****-****-1111"
    /// </code>
    /// </example>
    public override string ToString() => $"****-****-****-{GetLast4()}";
}
