using System.Security.Cryptography;
using System.Text;
using CardService.Application.Common;

namespace CardService.Infrastructure.Services;

/// <summary>
/// Implementation of the card number hasher port using SHA-256 with salt.
/// </summary>
/// <remarks>
/// <para>
/// This service provides secure hashing of card numbers for storage in the database.
/// </para>
/// <para>
/// Security Features:
/// <list type="bullet">
/// <item><strong>SHA-256:</strong> Industry-standard cryptographic hash algorithm (irreversible)</item>
/// <item><strong>Salting:</strong> A salt value is appended to the card number before hashing to protect against rainbow table attacks</item>
/// <item><strong>Deterministic:</strong> The same card number + salt always produces the same hash (enables duplicate detection)</item>
/// </list>
/// </para>
/// <para>
/// Production Security Notes:
/// <list type="bullet">
/// <item>The salt must be kept secure and should be a strong random value (e.g., 32 bytes)</item>
/// <item>The salt should be stored in a secret manager (e.g., AWS Secrets Manager, Azure Key Vault)</item>
/// <item>Never hardcode the salt in source code or commit it to version control</item>
/// <item>The salt must be identical across all application instances for consistent hashing</item>
/// </list>
/// </para>
/// </remarks>
public class CardNumberHasher : ICardNumberHasher
{
    private readonly string _salt;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardNumberHasher"/> class.
    /// </summary>
    /// <remarks>
    /// The salt is provided at dependency injection time and must not be empty or whitespace.
    /// In production, the salt should be retrieved from a secure configuration source.
    /// </remarks>
    /// <param name="salt">The salt value to use for hashing. Must not be null, empty, or whitespace.
    /// Typically a strong random string (e.g., 32 characters from /dev/urandom).</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="salt"/> is null, empty, or whitespace.</exception>
    public CardNumberHasher(string salt)
    {
        if (string.IsNullOrWhiteSpace(salt))
            throw new ArgumentException("Salt cannot be empty.", nameof(salt));

        _salt = salt;
    }

    /// <summary>
    /// Hashes the provided card number using SHA-256 with salt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hashing Process:
    /// <list type="number">
    /// <item>Concatenate the card number with the salt: <c>cardNumber + salt</c></item>
    /// <item>Encode the concatenated string as UTF-8 bytes</item>
    /// <item>Compute the SHA-256 hash of the bytes</item>
    /// <item>Convert the hash bytes to hexadecimal string representation</item>
    /// </list>
    /// </para>
    /// <para>
    /// The result is a deterministic 64-character hexadecimal string (256 bits in hex form)
    /// that can be safely stored in the database for uniqueness checking and duplicate detection.
    /// </para>
    /// </remarks>
    /// <param name="cardNumber">The plaintext 16-digit card number (e.g., "4111111111111111").
    /// This parameter is treated as highly sensitive.</param>
    /// <returns>The SHA-256 hash as a hexadecimal string (64 characters). Deterministic: same input always produces same hash.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="cardNumber"/> is null, empty, or whitespace.</exception>
    /// <example>
    /// <code>
    /// var hasher = new CardNumberHasher("my-production-salt-12345");
    /// var hash = hasher.Hash("4111111111111111");
    /// // hash might be: "a1b2c3d4e5f6...987654321" (64 hex characters)
    /// 
    /// // Same input always produces same hash (deterministic)
    /// var hash2 = hasher.Hash("4111111111111111");
    /// assert(hash == hash2); // true
    /// </code>
    /// </example>
    public string Hash(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            throw new ArgumentException("Card number cannot be empty.", nameof(cardNumber));

        var input = cardNumber + _salt;
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
