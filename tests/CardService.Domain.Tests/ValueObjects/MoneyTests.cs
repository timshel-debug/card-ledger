using CardService.Domain.ValueObjects;
using AwesomeAssertions;

namespace CardService.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void FromUsd_WithValidAmount_ShouldSucceed()
    {
        // Arrange & Act
        var money = Money.FromUsd(10.99m);

        // Assert
        money.AmountInCents.Should().Be(1099);
        money.Currency.Should().Be("USD");
        money.ToDecimal().Should().Be(10.99m);
    }

    [Theory]
    [InlineData(10.005, 1001)] // Rounds up (away from zero)
    [InlineData(10.004, 1000)] // Rounds down
    [InlineData(10.015, 1002)] // Rounds up
    public void FromUsd_ShouldRoundAwayFromZero(decimal input, long expectedCents)
    {
        // Act
        var money = Money.FromUsd(input);

        // Assert
        money.AmountInCents.Should().Be(expectedCents);
    }

    [Fact]
    public void ConvertTo_ShouldApplyExchangeRateAndRound()
    {
        // Arrange
        var money = Money.FromUsd(10.01m);
        var exchangeRate = 1.612m;

        // Act
        var converted = money.ConvertTo("AUD", exchangeRate);

        // Assert
        converted.ToDecimal().Should().Be(16.14m); // 10.01 * 1.612 = 16.13612, rounds to 16.14
        converted.Currency.Should().Be("AUD");
    }

    [Fact]
    public void FromUsd_WithNegativeAmount_ShouldThrow()
    {
        // Act & Assert
        var act = () => Money.FromUsd(-1.00m);
        act.Should().Throw<ArgumentException>();
    }
}
