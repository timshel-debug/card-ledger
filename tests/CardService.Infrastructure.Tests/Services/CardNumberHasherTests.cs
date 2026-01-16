using CardService.Infrastructure.Services;
using AwesomeAssertions;

namespace CardService.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for CardNumberHasher to verify secure hashing behavior.
/// </summary>
public class CardNumberHasherTests
{
    [Fact]
    public void Hash_SameInputAndSalt_ProducesSameHash()
    {
        // Arrange
        var hasher = new CardNumberHasher("test-salt-12345");
        var cardNumber = "4111111111111111";

        // Act
        var hash1 = hasher.Hash(cardNumber);
        var hash2 = hasher.Hash(cardNumber);

        // Assert
        hash1.Should().Be(hash2, "same input with same salt must produce same hash (deterministic)");
    }

    [Fact]
    public void Hash_SameInputDifferentSalt_ProducesDifferentHash()
    {
        // Arrange
        var hasher1 = new CardNumberHasher("salt-one");
        var hasher2 = new CardNumberHasher("salt-two");
        var cardNumber = "4111111111111111";

        // Act
        var hash1 = hasher1.Hash(cardNumber);
        var hash2 = hasher2.Hash(cardNumber);

        // Assert
        hash1.Should().NotBe(hash2, "different salts must produce different hashes");
    }

    [Fact]
    public void Hash_OutputIsUppercaseHexAnd64Characters()
    {
        // Arrange
        var hasher = new CardNumberHasher("test-salt");
        var cardNumber = "4111111111111111";

        // Act
        var hash = hasher.Hash(cardNumber);

        // Assert
        hash.Should().HaveLength(64, "SHA-256 hash in hex format is 64 characters");
        hash.Should().MatchRegex("^[0-9A-F]{64}$", "hash should be uppercase hex (Convert.ToHexString returns uppercase)");
    }

    [Fact]
    public void Hash_ValidCardNumber_DoesNotThrow()
    {
        // Arrange
        var hasher = new CardNumberHasher("test-salt");
        var cardNumber = "4111111111111111";

        // Act & Assert
        var exception = Record.Exception(() => hasher.Hash(cardNumber));
        exception.Should().BeNull("valid card number should hash without error");
    }

    [Fact]
    public void Hash_NullCardNumber_ThrowsArgumentException()
    {
        // Arrange
        var hasher = new CardNumberHasher("test-salt");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => hasher.Hash(null!));
        exception.Message.Should().Contain("Card number cannot be empty");
    }

    [Fact]
    public void Hash_EmptyCardNumber_ThrowsArgumentException()
    {
        // Arrange
        var hasher = new CardNumberHasher("test-salt");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => hasher.Hash(""));
        exception.Message.Should().Contain("Card number cannot be empty");
    }

    [Fact]
    public void Constructor_NullSalt_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new CardNumberHasher(null!));
        exception.Message.Should().Contain("Salt cannot be empty");
    }

    [Fact]
    public void Constructor_EmptySalt_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new CardNumberHasher(""));
        exception.Message.Should().Contain("Salt cannot be empty");
    }
}
