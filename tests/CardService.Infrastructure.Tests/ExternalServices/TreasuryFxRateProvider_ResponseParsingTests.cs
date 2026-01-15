using CardService.Infrastructure.ExternalServices;
using CardService.Infrastructure.Tests.TestDoubles;
using AwesomeAssertions;
using System.Globalization;

namespace CardService.Infrastructure.Tests.ExternalServices;

public class TreasuryFxRateProvider_ResponseParsingTests
{
    [Fact]
    public async Task GetLatestRateAsync_ParsesValidResponse_ReturnsExpectedFxRate()
    {
        // Arrange
        var handler = new CapturingHttpMessageHandler();
        var responseJson = @"{
            ""data"": [
                {
                    ""record_date"": ""2024-09-30"",
                    ""country_currency_desc"": ""Australia-Dollar"",
                    ""exchange_rate"": ""1.612""
                }
            ]
        }";
        handler.AddResponse(responseJson);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.fiscaldata.treasury.gov/services/api/fiscal_service/")
        };

        var provider = new TreasuryFxRateProvider(httpClient);

        // Act
        var result = await provider.GetLatestRateAsync("Australia-Dollar", new DateOnly(2024, 11, 01));

        // Assert
        result.Should().NotBeNull();
        result!.CurrencyKey.Should().Be("Australia-Dollar");
        result.RecordDate.Should().Be(new DateOnly(2024, 09, 30));
        result.ExchangeRate.Should().Be(1.612m);
    }

    [Fact]
    public async Task GetLatestRateAsync_ParsesDecimalCorrectly_InvariantCulture()
    {
        // Arrange
        var handler = new CapturingHttpMessageHandler();
        var responseJson = @"{
            ""data"": [
                {
                    ""record_date"": ""2024-06-30"",
                    ""country_currency_desc"": ""Euro-Zone-Euro"",
                    ""exchange_rate"": ""0.934567""
                }
            ]
        }";
        handler.AddResponse(responseJson);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.fiscaldata.treasury.gov/services/api/fiscal_service/")
        };

        var provider = new TreasuryFxRateProvider(httpClient);

        // Act
        var result = await provider.GetLatestRateAsync("Euro-Zone-Euro", new DateOnly(2024, 12, 31));

        // Assert - Verify exact decimal parsing with no culture-dependent rounding
        result.Should().NotBeNull();
        result!.ExchangeRate.Should().Be(0.934567m);
    }

    [Fact]
    public async Task GetLatestRateAsync_EmptyDataArray_ReturnsNull()
    {
        // Arrange
        var handler = new CapturingHttpMessageHandler();
        var responseJson = @"{""data"": []}";
        handler.AddResponse(responseJson);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.fiscaldata.treasury.gov/services/api/fiscal_service/")
        };

        var provider = new TreasuryFxRateProvider(httpClient);

        // Act
        var result = await provider.GetLatestRateAsync("NonExistent-Currency", new DateOnly(2024, 01, 01));

        // Assert - Per provider contract, returns null when no data found
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestRateAsync_MissingDataProperty_ReturnsNull()
    {
        // Arrange
        var handler = new CapturingHttpMessageHandler();
        var responseJson = @"{""meta"": {}}";
        handler.AddResponse(responseJson);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.fiscaldata.treasury.gov/services/api/fiscal_service/")
        };

        var provider = new TreasuryFxRateProvider(httpClient);

        // Act
        var result = await provider.GetLatestRateAsync("Test-Currency", new DateOnly(2024, 01, 01));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestRateAsync_NullOrEmptyRequiredFields_ReturnsNull()
    {
        // Arrange
        var handler = new CapturingHttpMessageHandler();
        var responseJson = @"{
            ""data"": [
                {
                    ""record_date"": """",
                    ""exchange_rate"": ""1.5""
                }
            ]
        }";
        handler.AddResponse(responseJson);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.fiscaldata.treasury.gov/services/api/fiscal_service/")
        };

        var provider = new TreasuryFxRateProvider(httpClient);

        // Act
        var result = await provider.GetLatestRateAsync("Test-Currency", new DateOnly(2024, 01, 01));

        // Assert - Provider should return null if required fields are empty/null
        result.Should().BeNull();
    }
}
