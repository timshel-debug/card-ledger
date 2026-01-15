using CardService.Domain.ValueObjects;

namespace CardService.Domain.Entities;

/// <summary>
/// Represents a credit or debit card as an aggregate root in the domain model.
/// </summary>
/// <remarks>
/// <para>Card is the aggregate root that owns <see cref="PurchaseTransaction"/> entities.</para>
/// <para>Enforces domain invariants: credit limit is positive and in USD, card number hash is non-empty.</para>
/// <para>The card number itself is never stored in plaintext; only the hash and last 4 digits are persisted.</para>
/// </remarks>
public sealed class Card
{
    /// <summary>
    /// Gets the unique identifier for this card.
    /// </summary>
    /// <value>A GUID that uniquely identifies the card.</value>
    public Guid Id { get; private set; }
    
    /// <summary>
    /// Gets the SHA-256 hash of the card number (salted) for secure storage.
    /// </summary>
    /// <value>The 64-character hexadecimal SHA-256 hash.</value>
    /// <remarks>Never exposes the actual card number; used for duplicate detection.</remarks>
    public string CardNumberHash { get; private set; } = string.Empty;
    
    /// <summary>
    /// Gets the last 4 digits of the card number for display purposes.
    /// </summary>
    /// <value>A 4-digit string (e.g., "1111").</value>
    public string Last4 { get; private set; } = string.Empty;
    
    /// <summary>
    /// Gets the card's credit limit in cents.
    /// </summary>
    /// <value>The credit limit as a non-negative 64-bit integer representing cents.</value>
    public long CreditLimitCents { get; private set; }
    
    /// <summary>
    /// Gets the UTC date and time when this card was created.
    /// </summary>
    /// <value>A <see cref="DateTime"/> in UTC.</value>
    public DateTime CreatedUtc { get; private set; }

    private readonly List<PurchaseTransaction> _purchases = new();
    
    /// <summary>
    /// Gets a read-only collection of all purchase transactions associated with this card.
    /// </summary>
    /// <value>An immutable view of the purchases.</value>
    public IReadOnlyCollection<PurchaseTransaction> Purchases => _purchases.AsReadOnly();

    /// <summary>
    /// Initializes a new instance of the <see cref="Card"/> class for Entity Framework Core.
    /// </summary>
    /// <remarks>This parameterless constructor is required by EF Core for materializing entities from the database.</remarks>
    private Card() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Card"/> class with the specified parameters.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="cardNumberHash">The SHA-256 hash of the card number.</param>
    /// <param name="last4">The last 4 digits of the card number.</param>
    /// <param name="creditLimitCents">The credit limit in cents.</param>
    /// <param name="createdUtc">The creation timestamp in UTC.</param>
    private Card(Guid id, string cardNumberHash, string last4, long creditLimitCents, DateTime createdUtc)
    {
        Id = id;
        CardNumberHash = cardNumberHash;
        Last4 = last4;
        CreditLimitCents = creditLimitCents;
        CreatedUtc = createdUtc;
    }

    /// <summary>
    /// Creates a new card with the specified parameters, enforcing domain invariants.
    /// </summary>
    /// <param name="cardNumber">The validated 16-digit card number.</param>
    /// <param name="creditLimit">The credit limit in USD.</param>
    /// <param name="cardNumberHash">The pre-computed SHA-256 hash of the card number (with salt).</param>
    /// <param name="createdUtc">The creation timestamp in UTC.</param>
    /// <returns>A new <see cref="Card"/> instance with a generated unique ID.</returns>
    /// <exception cref="ArgumentException">Thrown when credit limit is not in USD, not positive, or card number hash is empty.</exception>
    /// <remarks>
    /// <para>This factory method validates domain invariants before creating the card.</para>
    /// <para>The card number is not stored in plaintext; only the provided hash is retained.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var cardNumber = CardNumber.Create("4111111111111111");
    /// var creditLimit = Money.FromUsd(5000m);
    /// var hash = ComputeCardHash(cardNumber.Value, salt);
    /// var card = Card.Create(cardNumber, creditLimit, hash, DateTime.UtcNow);
    /// </code>
    /// </example>
    public static Card Create(CardNumber cardNumber, Money creditLimit, string cardNumberHash, DateTime createdUtc)
    {
        if (creditLimit.Currency != "USD")
            throw new ArgumentException("Credit limit must be in USD.", nameof(creditLimit));

        if (creditLimit.AmountInCents <= 0)
            throw new ArgumentException("Credit limit must be greater than zero.", nameof(creditLimit));

        if (string.IsNullOrWhiteSpace(cardNumberHash))
            throw new ArgumentException("Card number hash is required.", nameof(cardNumberHash));

        return new Card(
            Guid.NewGuid(),
            cardNumberHash,
            cardNumber.GetLast4(),
            creditLimit.AmountInCents,
            createdUtc
        );
    }

    /// <summary>
    /// Adds a purchase transaction to this card.
    /// </summary>
    /// <param name="description">The purchase description (max 50 characters).</param>
    /// <param name="transactionDate">The date on which the purchase was made.</param>
    /// <param name="amount">The purchase amount in USD.</param>
    /// <param name="createdUtc">The timestamp when the purchase record was created (UTC).</param>
    /// <returns>The newly created <see cref="PurchaseTransaction"/>.</returns>
    /// <remarks>
    /// <para>The purchase is automatically added to the card's internal collection.</para>
    /// <para>Validation of individual purchase parameters is delegated to <see cref="PurchaseTransaction.Create"/>.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var purchase = card.AddPurchase("Groceries", new DateOnly(2024, 12, 25), Money.FromUsd(50.00m), DateTime.UtcNow);
    /// </code>
    /// </example>
    public PurchaseTransaction AddPurchase(string description, DateOnly transactionDate, Money amount, DateTime createdUtc)
    {
        var purchase = PurchaseTransaction.Create(Id, description, transactionDate, amount, createdUtc);
        _purchases.Add(purchase);
        return purchase;
    }

    /// <summary>
    /// Computes the available balance on the card in USD.
    /// </summary>
    /// <returns>A <see cref="Money"/> instance representing the available balance (credit limit minus purchases).</returns>
    /// <remarks>
    /// <para>Available balance = credit limit - sum of all purchase amounts.</para>
    /// <para>If total purchases exceed the credit limit, returns zero (never negative).</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var available = card.GetAvailableBalance();
    /// Console.WriteLine(available.ToDecimal()); // 4950.00 if $5000 limit and $50 in purchases
    /// </code>
    /// </example>
    public Money GetAvailableBalance()
    {
        var totalPurchases = _purchases.Sum(p => p.AmountCents);
        var availableBalance = CreditLimitCents - totalPurchases;
        return Money.FromCents(Math.Max(0, availableBalance), "USD");
    }
}
