namespace CardService.Application.Common;

/// <summary>
/// Port interface for hashing card numbers for secure storage and uniqueness verification.
/// </summary>
/// <remarks>
/// <para>
/// Card numbers are sensitive PII (Personally Identifiable Information) and must never be stored
/// in plaintext. This interface defines the contract for securely hashing card numbers using
/// SHA-256, enabling:
/// <list type="bullet">
/// <item>Secure storage (only hash is persisted in the database)</item>
/// <item>Duplicate detection (hash-based uniqueness constraint)</item>
/// <item>Irreversibility (hash cannot be reversed to recover the original card number)</item>
/// </list>
/// </para>
/// <para>
/// Implementations must use cryptographically secure algorithms (e.g., SHA-256) and, in production,
/// should apply additional security measures such as salting to further protect against rainbow table attacks.
/// </para>
/// </remarks>
public interface ICardNumberHasher
{
    /// <summary>
    /// Hashes the provided card number using a secure cryptographic algorithm (SHA-256).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method takes the plaintext card number and returns a deterministic hash suitable for:
    /// <list type="bullet">
    /// <item>Storing in the database as <c>card_number_hash</c></item>
    /// <item>Querying for duplicates via <see cref="ICardRepository.ExistsByHashAsync"/></item>
    /// <item>Comparing against previously hashed numbers to detect card reuse</item>
    /// </list>
    /// </para>
    /// <para>
    /// The hash is deterministic: the same card number always produces the same hash.
    /// This enables duplicate detection without storing the plaintext card number.
    /// </para>
    /// </remarks>
    /// <param name="cardNumber">The plaintext 16-digit card number to hash (e.g., "4111111111111111").
    /// This parameter should be treated as highly sensitive and cleared from memory after hashing in production implementations.</param>
    /// <returns>The SHA-256 hexadecimal hash of the card number, suitable for database storage.
    /// Typically a 64-character string (256 bits represented as hexadecimal).</returns>
    /// <example>
    /// <code>
    /// var hasher = new CardNumberHasher();
    /// string cardNumber = "4111111111111111";
    /// string hash = hasher.Hash(cardNumber);
    /// // hash == "4f53cda18c2baa0c0354bb5f4a8d8d6560ad8a57ee5a8ba95f27abb99b8b6aa" (example)
    /// </code>
    /// </example>
    string Hash(string cardNumber);
}
