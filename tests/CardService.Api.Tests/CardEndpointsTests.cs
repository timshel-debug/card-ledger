using System.Net;
using System.Net.Http.Json;
using CardService.Application.DTOs;
using AwesomeAssertions;

namespace CardService.Api.Tests;

public class CardEndpointsTests : IntegrationTestBase
{
    public CardEndpointsTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateCard_WithValidData_ShouldReturn201()
    {
        // Arrange
        var request = new CreateCardRequest("4111111111111111", 1000.00m);

        // Act
        var response = await Client.PostAsJsonAsync("/cards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateCardResponse>();
        result.Should().NotBeNull();
        result!.CardId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateCard_WithInvalidCardNumber_ShouldReturn400()
    {
        // Arrange
        var request = new CreateCardRequest("411111111111111", 1000.00m); // 15 digits

        // Act
        var response = await Client.PostAsJsonAsync("/cards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCard_WithDuplicateCardNumber_ShouldReturn409()
    {
        // Arrange
        var request = new CreateCardRequest("4111111111111111", 1000.00m);

        // Act
        await Client.PostAsJsonAsync("/cards", request); // First create
        var response = await Client.PostAsJsonAsync("/cards", request); // Duplicate

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetBalance_ForExistingCard_ShouldReturnBalance()
    {
        // Arrange
        var createResponse = await Client.PostAsJsonAsync("/cards", 
            new CreateCardRequest("4111111111111111", 1000.00m));
        var card = await createResponse.Content.ReadFromJsonAsync<CreateCardResponse>();

        // Act
        var response = await Client.GetAsync($"/cards/{card!.CardId}/balance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var balance = await response.Content.ReadFromJsonAsync<BalanceResponse>();
        balance.Should().NotBeNull();
        balance!.CreditLimitUsd.Should().Be(1000.00m);
        balance.AvailableBalanceUsd.Should().Be(1000.00m);
    }

    [Fact]
    public async Task GetBalance_ForNonExistentCard_ShouldReturn404()
    {
        // Act
        var response = await Client.GetAsync($"/cards/{Guid.NewGuid()}/balance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
