using CardService.Domain.ValueObjects;

namespace CardService.Domain.Entities;

/// <summary>
/// Represents a purchase transaction on a card as an entity within the <see cref="Card"/> aggregate.
/// </summary>
/// <remarks>
/// <para>PurchaseTransaction is owned by the <see cref="Card"/> aggregate root and cannot exist independently.</para>
/// <para>Each purchase records the amount in cents to maintain precision and avoid floating-point errors.</para>
/// <para>Purchases must have valid USD amounts and non-empty descriptions (max 50 characters).</para>
/// </remarks>
public sealed class PurchaseTransaction
{
    /// <summary>
    /// Gets the unique identifier for this purchase transaction.
    /// </summary>
    /// <value>A GUID that uniquely identifies the purchase.</value>
    public Guid Id { get; private set; }
    
    /// <summary>
    /// Gets the unique identifier of the <see cref="Card"/> that owns this purchase.
    /// </summary>
    /// <value>A GUID referencing the parent card.</value>
    public Guid CardId { get; private set; }
    
    /// <summary>
    /// Gets the description of the purchase (e.g., "Groceries", "Gas Station").
    /// </summary>
    /// <value>A non-empty string with a maximum length of 50 characters.</value>
    public string Description { get; private set; } = string.Empty;
    
    /// <summary>
    /// Gets the date on which the purchase was made.
    /// </summary>
    /// <value>A <see cref="DateOnly"/> representing the transaction date.</value>
    public DateOnly TransactionDate { get; private set; }
    
    /// <summary>
    /// Gets the purchase amount in cents (USD).
    /// </summary>
    /// <value>A positive 64-bit integer representing cents.</value>
    public long AmountCents { get; private set; }
    
    /// <summary>
    /// Gets the UTC date and time when this purchase record was created.
    /// </summary>
    /// <value>A <see cref="DateTime"/> in UTC.</value>
    public DateTime CreatedUtc { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PurchaseTransaction"/> class for Entity Framework Core.
    /// </summary>
    /// <remarks>This parameterless constructor is required by EF Core for materializing entities from the database.</remarks>
    private PurchaseTransaction() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PurchaseTransaction"/> class with the specified parameters.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="cardId">The unique identifier of the owning card.</param>
    /// <param name="description">The purchase description.</param>
    /// <param name="transactionDate">The date when the purchase occurred.</param>
    /// <param name="amountCents">The purchase amount in cents.</param>
    /// <param name="createdUtc">The creation timestamp in UTC.</param>
    private PurchaseTransaction(Guid id, Guid cardId, string description, DateOnly transactionDate, long amountCents, DateTime createdUtc)
    {
        Id = id;
        CardId = cardId;
        Description = description;
        TransactionDate = transactionDate;
        AmountCents = amountCents;
        CreatedUtc = createdUtc;
    }

    /// <summary>
    /// Creates a new purchase transaction with the specified parameters, enforcing domain invariants.
    /// </summary>
    /// <param name="cardId">The unique identifier of the owning card (must not be empty).</param>
    /// <param name="description">The purchase description (non-empty, max 50 characters).</param>
    /// <param name="transactionDate">The date when the purchase was made.</param>
    /// <param name="amount">The purchase amount in USD (must be positive).</param>
    /// <param name="createdUtc">The creation timestamp in UTC.</param>
    /// <returns>A new <see cref="PurchaseTransaction"/> instance with a generated unique ID.</returns>
    /// <exception cref="ArgumentException">Thrown when card ID is empty, description is invalid, amount is not USD, or amount is not positive.</exception>
    /// <remarks>
    /// <para>This factory method validates all domain invariants before creating the purchase.</para>
    /// <para>The amount is stored as cents internally; only USD amounts are accepted.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var purchase = PurchaseTransaction.Create(
    ///     cardId: cardGuid,
    ///     description: "Coffee Shop",
    ///     transactionDate: new DateOnly(2024, 12, 25),
    ///     amount: Money.FromUsd(5.50m),
    ///     createdUtc: DateTime.UtcNow
    /// );
    /// </code>
    /// </example>
    public static PurchaseTransaction Create(Guid cardId, string description, DateOnly transactionDate, Money amount, DateTime createdUtc)
    {
        if (cardId == Guid.Empty)
            throw new ArgumentException("Card ID cannot be empty.", nameof(cardId));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty.", nameof(description));

        if (description.Length > 50)
            throw new ArgumentException("Description cannot exceed 50 characters.", nameof(description));

        if (amount.Currency != "USD")
            throw new ArgumentException("Purchase amount must be in USD.", nameof(amount));

        if (amount.AmountInCents <= 0)
            throw new ArgumentException("Purchase amount must be greater than zero.", nameof(amount));

        return new PurchaseTransaction(
            Guid.NewGuid(),
            cardId,
            description,
            transactionDate,
            amount.AmountInCents,
            createdUtc
        );
    }

    /// <summary>
    /// Gets the purchase amount as a <see cref="Money"/> value object.
    /// </summary>
    /// <returns>A <see cref="Money"/> instance representing the purchase amount in USD.</returns>
    /// <example>
    /// <code>
    /// var amount = purchase.GetAmount();
    /// Console.WriteLine(amount.ToDecimal()); // 5.50
    /// </code>
    /// </example>
    public Money GetAmount() => Money.FromCents(AmountCents, "USD");
}
