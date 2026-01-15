using System.Net;
using System.Net.Http.Json;
using CardService.Application.DTOs;
using AwesomeAssertions;

namespace CardService.Api.Tests;

public class FxConversionAcceptanceTests : IntegrationTestBase
{
    public FxConversionAcceptanceTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task SixMonthBoundary_ExactlySixMonthsPrior_ShouldBeIncluded()
    {
        // Arrange
        Factory.FakeTreasuryProvider.Clear();
        
        // Purchase on 2024-12-31, rate exactly 6 months prior (2024-06-30)
        var purchaseDate = new DateOnly(2024, 12, 31);
        var rateDate = new DateOnly(2024, 6, 30); // Exactly 6 months prior
        
        Factory.FakeTreasuryProvider.AddRate("Test-Currency", rateDate, 1.5m);

        var card = await CreateTestCard();
        var purchase = await CreateTestPurchase(card.CardId, 100.00m, purchaseDate);

        // Act
        var response = await Client.GetAsync(
            $"/cards/{card.CardId}/purchases/{purchase.PurchaseId}?currencyKey=Test-Currency");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ConvertedPurchaseResponse>();
        result.Should().NotBeNull();
        result!.RateDate.Should().Be(rateDate);
        result.ExchangeRate.Should().Be(1.5m);
    }

    [Fact]
    public async Task SixMonthBoundary_OneDayBeforeSixMonths_ShouldBeExcluded()
    {
        // Arrange
        Factory.FakeTreasuryProvider.Clear();
        
        // Purchase on 2024-12-31, rate at 2024-06-29 (6 months + 1 day)
        var purchaseDate = new DateOnly(2024, 12, 31);
        var rateDate = new DateOnly(2024, 6, 29);
        
        Factory.FakeTreasuryProvider.AddRate("Test-Currency", rateDate, 1.5m);

        var card = await CreateTestCard();
        var purchase = await CreateTestPurchase(card.CardId, 100.00m, purchaseDate);

        // Act
        var response = await Client.GetAsync(
            $"/cards/{card.CardId}/purchases/{purchase.PurchaseId}?currencyKey=Test-Currency");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CacheFallback_WhenUpstreamFailsButCacheHasRate_ShouldSucceed()
    {
        // Arrange
        Factory.FakeTreasuryProvider.Clear();
        Factory.FakeTreasuryProvider.AddRate("Test-Currency", new DateOnly(2024, 12, 15), 1.5m);

        var card = await CreateTestCard();
        var purchase = await CreateTestPurchase(card.CardId, 100.00m, new DateOnly(2024, 12, 20));

        // First request to cache the rate
        var firstResponse = await Client.GetAsync(
            $"/cards/{card.CardId}/purchases/{purchase.PurchaseId}?currencyKey=Test-Currency");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Now simulate upstream failure
        Factory.FakeTreasuryProvider.ShouldThrow = true;

        // Act - should use cache
        var response = await Client.GetAsync(
            $"/cards/{card.CardId}/purchases/{purchase.PurchaseId}?currencyKey=Test-Currency");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ConvertedPurchaseResponse>();
        result.Should().NotBeNull();
        result!.ExchangeRate.Should().Be(1.5m);
    }

    [Fact]
    public async Task RoundingBehavior_ShouldRoundAwayFromZero()
    {
        // Arrange
        Factory.FakeTreasuryProvider.Clear();
        Factory.FakeTreasuryProvider.AddRate("Test-Currency", new DateOnly(2024, 12, 15), 1.612m);

        var card = await CreateTestCard();
        var purchase = await CreateTestPurchase(card.CardId, 10.01m, new DateOnly(2024, 12, 20));

        // Act
        var response = await Client.GetAsync(
            $"/cards/{card.CardId}/purchases/{purchase.PurchaseId}?currencyKey=Test-Currency");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ConvertedPurchaseResponse>();
        result.Should().NotBeNull();
        
        // 10.01 * 1.612 = 16.13612
        // Should round to 16.14 (away from zero)
        result!.ConvertedAmount.Should().Be(16.14m);
    }

    private async Task<CreateCardResponse> CreateTestCard(decimal creditLimit = 1000.00m)
    {
        var cardNumber = $"4111111111{Random.Shared.Next(100000, 999999)}";
        var response = await Client.PostAsJsonAsync("/cards", 
            new CreateCardRequest(cardNumber, creditLimit));
        return (await response.Content.ReadFromJsonAsync<CreateCardResponse>())!;
    }

    private async Task<CreatePurchaseResponse> CreateTestPurchase(Guid cardId, decimal amount, DateOnly date)
    {
        var response = await Client.PostAsJsonAsync($"/cards/{cardId}/purchases",
            new CreatePurchaseRequest("Test Purchase", date, amount));
        return (await response.Content.ReadFromJsonAsync<CreatePurchaseResponse>())!;
    }
}
