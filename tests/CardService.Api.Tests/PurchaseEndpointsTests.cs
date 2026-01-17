using System.Net;
using System.Net.Http.Json;
using CardService.Application.DTOs;
using AwesomeAssertions;

namespace CardService.Api.Tests;

public class PurchaseEndpointsTests : IntegrationTestBase
{
    public PurchaseEndpointsTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreatePurchase_WithValidData_ShouldReturn201()
    {
        // Arrange
        var card = await CreateTestCard();
        var request = new CreatePurchaseRequest("Test Purchase", new DateOnly(2024, 12, 15), 100.00m);

        // Act
        var response = await Client.PostAsJsonAsync($"/cards/{card.CardId}/purchases", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreatePurchaseResponse>();
        result.Should().NotBeNull();
        result!.PurchaseId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreatePurchase_WithDescriptionOver50Chars_ShouldReturn400()
    {
        // Arrange
        var card = await CreateTestCard();
        var longDescription = new string('a', 51);
        var request = new CreatePurchaseRequest(longDescription, new DateOnly(2024, 12, 15), 100.00m);

        // Act
        var response = await Client.PostAsJsonAsync($"/cards/{card.CardId}/purchases", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePurchase_ForNonExistentCard_ShouldReturn404()
    {
        // Arrange
        var request = new CreatePurchaseRequest("Test", new DateOnly(2024, 12, 15), 100.00m);

        // Act
        var response = await Client.PostAsJsonAsync($"/cards/{Guid.NewGuid()}/purchases", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPurchaseConverted_WithAvailableRate_ShouldReturnConvertedPurchase()
    {
        // Arrange
        Factory.FakeTreasuryProvider.Clear();
        Factory.FakeTreasuryProvider.AddRate("Australia-Dollar", new DateOnly(2024, 12, 15), 1.612m);

        var card = await CreateTestCard();
        var purchase = await CreateTestPurchase(card.CardId, 10.01m, new DateOnly(2024, 12, 20));

        // Act
        var response = await Client.GetAsync(
            $"/cards/{card.CardId}/purchases/{purchase.PurchaseId}?currencyKey=Australia-Dollar");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ConvertedPurchaseResponse>();
        result.Should().NotBeNull();
        result!.AmountUsd.Should().Be(10.01m);
        result.ExchangeRate.Should().Be(1.612m);
        result.ConvertedAmount.Should().Be(16.14m); // 10.01 * 1.612 rounded
        result.CurrencyKey.Should().Be("Australia-Dollar");
    }

    [Fact]
    public async Task GetPurchaseConverted_WithNoRateInWindow_ShouldReturn422()
    {
        // Arrange
        Factory.FakeTreasuryProvider.Clear();
        Factory.FakeTreasuryProvider.AddRate("Australia-Dollar", new DateOnly(2024, 1, 1), 1.5m); // Too old

        var card = await CreateTestCard();
        var purchase = await CreateTestPurchase(card.CardId, 100.00m, new DateOnly(2024, 12, 20));

        // Act
        var response = await Client.GetAsync(
            $"/cards/{card.CardId}/purchases/{purchase.PurchaseId}?currencyKey=Australia-Dollar");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetPurchaseConverted_WithUpstreamFailureAndNoCache_ShouldReturn503()
    {
        // Arrange
        Factory.FakeTreasuryProvider.Clear();
        Factory.FakeTreasuryProvider.ShouldThrow = true;

        var card = await CreateTestCard();
        var purchase = await CreateTestPurchase(card.CardId, 100.00m, new DateOnly(2024, 12, 20));

        // Act
        var response = await Client.GetAsync(
            $"/cards/{card.CardId}/purchases/{purchase.PurchaseId}?currencyKey=Australia-Dollar");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetPurchaseConverted_SelectsLatestRateWithinWindow()
    {
        // Arrange
        Factory.FakeTreasuryProvider.Clear();
        Factory.FakeTreasuryProvider.AddRate("Australia-Dollar", new DateOnly(2024, 9, 30), 1.5m);
        Factory.FakeTreasuryProvider.AddRate("Australia-Dollar", new DateOnly(2024, 11, 1), 1.6m);
        Factory.FakeTreasuryProvider.AddRate("Australia-Dollar", new DateOnly(2024, 12, 31), 1.7m); // Future, should not be selected

        var card = await CreateTestCard();
        var purchase = await CreateTestPurchase(card.CardId, 100.00m, new DateOnly(2024, 11, 15));

        // Act
        var response = await Client.GetAsync(
            $"/cards/{card.CardId}/purchases/{purchase.PurchaseId}?currencyKey=Australia-Dollar");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ConvertedPurchaseResponse>();
        result.Should().NotBeNull();
        result!.ExchangeRate.Should().Be(1.6m); // Latest rate <= purchase date
        result.RateDate.Should().Be(new DateOnly(2024, 11, 1));
    }

    [Fact]
    public async Task GetBalance_WithConversion_ShouldReturnConvertedBalance()
    {
        // Arrange
        Factory.FakeTreasuryProvider.Clear();
        Factory.FakeTreasuryProvider.AddRate("Australia-Dollar", new DateOnly(2024, 12, 31), 1.5m);

        var card = await CreateTestCard(1000.00m);
        await CreateTestPurchase(card.CardId, 200.00m, new DateOnly(2024, 12, 20));
        await CreateTestPurchase(card.CardId, 300.00m, new DateOnly(2024, 12, 25));

        // Act
        var response = await Client.GetAsync(
            $"/cards/{card.CardId}/balance?currencyKey=Australia-Dollar");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        result.Should().NotBeNull();
        result!.CreditLimitUsd.Should().Be(1000.00m);
        result.TotalPurchasesUsd.Should().Be(500.00m);
        result.AvailableBalanceUsd.Should().Be(500.00m);
        result.ConvertedAvailableBalance.Should().Be(750.00m); // 500 * 1.5
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

    [Fact]
    public async Task GetBalance_WhenPurchasesExceedCreditLimit_ShouldReturnNegativeBalance()
    {
        // Arrange
        var card = await CreateTestCard(100.00m);
        await CreateTestPurchase(card.CardId, 150.00m, new DateOnly(2024, 12, 20));

        // Act
        var response = await Client.GetAsync($"/cards/{card.CardId}/balance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        result.Should().NotBeNull();
        result!.CreditLimitUsd.Should().Be(100.00m);
        result.TotalPurchasesUsd.Should().Be(150.00m);
        result.AvailableBalanceUsd.Should().Be(-50.00m); // 100 - 150 = -50
    }

    [Fact]
    public async Task GetBalance_WhenPurchasesExceedCreditLimit_WithConversion_ShouldReturnNegativeConvertedBalance()
    {
        // Arrange
        Factory.FakeTreasuryProvider.Clear();
        Factory.FakeTreasuryProvider.AddRate("Test-Currency", new DateOnly(2024, 12, 31), 2.0m);

        var card = await CreateTestCard(100.00m);
        await CreateTestPurchase(card.CardId, 150.00m, new DateOnly(2024, 12, 20));

        // Act
        var response = await Client.GetAsync(
            $"/cards/{card.CardId}/balance?currencyKey=Test-Currency");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        result.Should().NotBeNull();
        result!.CreditLimitUsd.Should().Be(100.00m);
        result.TotalPurchasesUsd.Should().Be(150.00m);
        result.AvailableBalanceUsd.Should().Be(-50.00m);
        result.ConvertedAvailableBalance.Should().Be(-100.00m); // -50 * 2.0 = -100
    }
}
