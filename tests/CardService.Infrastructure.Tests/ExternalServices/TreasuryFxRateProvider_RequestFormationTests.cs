using CardService.Infrastructure.ExternalServices;
using CardService.Infrastructure.Tests.TestDoubles;
using AwesomeAssertions;
using System.Web;

namespace CardService.Infrastructure.Tests.ExternalServices;

public class TreasuryFxRateProvider_RequestFormationTests
{
    [Fact]
    public async Task GetLatestRateAsync_BuildsCorrectQueryParameters()
    {
        // Arrange
        var handler = new CapturingHttpMessageHandler();
        handler.AddResponse(@"{""data"":[{""record_date"":""2024-09-30"",""exchange_rate"":""1.5""}]}");

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.fiscaldata.treasury.gov/services/api/fiscal_service/")
        };

        var provider = new TreasuryFxRateProvider(httpClient);
        var currencyKey = "Australia-Dollar";
        var anchorDate = new DateOnly(2024, 11, 01);

        // Act
        await provider.GetLatestRateAsync(currencyKey, anchorDate, monthsBack: 6);

        // Assert
        var request = handler.LastRequest;
        request.Should().NotBeNull();
        request!.Method.Should().Be(HttpMethod.Get);
        request.RequestUri.Should().NotBeNull();
        
        // Verify endpoint path
        request.RequestUri!.AbsolutePath.Should().Contain("v1/accounting/od/rates_of_exchange");

        // Parse and verify query parameters
        var query = request.RequestUri.Query;
        var queryParams = HttpUtility.ParseQueryString(query);

        // Verify filter parameter contains all required constraints
        var filter = queryParams["filter"];
        filter.Should().NotBeNullOrEmpty();
        filter.Should().Contain("record_date:lte:2024-11-01", "must filter by anchor date upper bound");
        filter.Should().Contain("record_date:gte:2024-05-01", "must filter by 6-month window lower bound");
        filter.Should().Contain("country_currency_desc:eq:Australia-Dollar", "must filter by currency key");

        // Verify sort descending by record_date
        var sort = queryParams["sort"];
        sort.Should().Be("-record_date", "must sort descending by record_date to get latest first");

        // Verify page size limit
        var pageSize = queryParams["page[size]"];
        pageSize.Should().Be("1", "must limit to 1 result");
    }

    [Fact]
    public async Task GetLatestRateAsync_UsesCorrectMonthsBackWindow()
    {
        // Arrange
        var handler = new CapturingHttpMessageHandler();
        handler.AddResponse(@"{""data"":[]}");

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.fiscaldata.treasury.gov/services/api/fiscal_service/")
        };

        var provider = new TreasuryFxRateProvider(httpClient);
        var anchorDate = new DateOnly(2024, 12, 31);

        // Act - using 3 months back instead of default 6
        await provider.GetLatestRateAsync("Test-Currency", anchorDate, monthsBack: 3);

        // Assert
        var request = handler.LastRequest;
        var query = request!.RequestUri!.Query;
        var queryParams = HttpUtility.ParseQueryString(query);
        var filter = queryParams["filter"];

        // 3 months back from 2024-12-31 is 2024-09-30
        filter.Should().Contain("record_date:gte:2024-09-30", "must respect custom monthsBack parameter");
    }
}
