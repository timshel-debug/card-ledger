using CardService.Domain.Entities;
using CardService.Domain.ValueObjects;
using AwesomeAssertions;

namespace CardService.Domain.Tests.Entities;

public class PurchaseTransactionTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var cardId = Guid.NewGuid();
        var description = "Test Purchase";
        var transactionDate = new DateOnly(2024, 1, 15);
        var amount = Money.FromUsd(100.00m);
        var createdUtc = DateTime.UtcNow;

        // Act
        var purchase = PurchaseTransaction.Create(cardId, description, transactionDate, amount, createdUtc);

        // Assert
        purchase.Id.Should().NotBeEmpty();
        purchase.CardId.Should().Be(cardId);
        purchase.Description.Should().Be(description);
        purchase.TransactionDate.Should().Be(transactionDate);
        purchase.AmountCents.Should().Be(10000);
    }

    [Fact]
    public void Create_WithDescriptionOver50Chars_ShouldThrow()
    {
        // Arrange
        var cardId = Guid.NewGuid();
        var description = new string('a', 51);
        var transactionDate = new DateOnly(2024, 1, 15);
        var amount = Money.FromUsd(100.00m);
        var createdUtc = DateTime.UtcNow;

        // Act & Assert
        var act = () => PurchaseTransaction.Create(cardId, description, transactionDate, amount, createdUtc);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*50 characters*");
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldThrow()
    {
        // Arrange
        var cardId = Guid.NewGuid();
        var description = "Test";
        var transactionDate = new DateOnly(2024, 1, 15);
        var amount = Money.FromCents(0, "USD");
        var createdUtc = DateTime.UtcNow;

        // Act & Assert
        var act = () => PurchaseTransaction.Create(cardId, description, transactionDate, amount, createdUtc);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*greater than zero*");
    }
}
