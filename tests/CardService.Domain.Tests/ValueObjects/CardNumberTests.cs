using CardService.Domain.ValueObjects;
using AwesomeAssertions;

namespace CardService.Domain.Tests.ValueObjects;

public class CardNumberTests
{
    [Fact]
    public void Create_WithValidCardNumber_ShouldSucceed()
    {
        // Arrange
        var validNumber = "4111111111111111";

        // Act
        var cardNumber = CardNumber.Create(validNumber);

        // Assert
        cardNumber.Value.Should().Be(validNumber);
    }

    [Fact]
    public void Create_WithNon16Digits_ShouldThrow()
    {
        // Arrange
        var invalidNumber = "411111111111111"; // 15 digits

        // Act & Assert
        var act = () => CardNumber.Create(invalidNumber);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*16 digits*");
    }

    [Fact]
    public void Create_WithNonNumeric_ShouldThrow()
    {
        // Arrange
        var invalidNumber = "411111111111111A";

        // Act & Assert
        var act = () => CardNumber.Create(invalidNumber);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*only digits*");
    }

    [Fact]
    public void GetLast4_ShouldReturnLastFourDigits()
    {
        // Arrange
        var cardNumber = CardNumber.Create("4111111111111234");

        // Act
        var last4 = cardNumber.GetLast4();

        // Assert
        last4.Should().Be("1234");
    }
}
