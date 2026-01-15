using CardService.Application.Common;
using CardService.Domain.Entities;
using CardService.Infrastructure.Repositories;
using CardService.Infrastructure.Tests.TestHelpers;
using AwesomeAssertions;

namespace CardService.Infrastructure.Tests.Repositories;

public class FxRateCacheRepositoryTests
{
    private class FixedClock : IClock
    {
        public DateTime UtcNow { get; }
        public DateOnly UtcToday => DateOnly.FromDateTime(UtcNow);
        public FixedClock(DateTime utcNow) => UtcNow = utcNow;
    }

    [Fact]
    public async Task GetLatestRateAsync_ReturnsLatestRecordDateWithinWindow()
    {
        // Arrange
        using var db = new SqliteInMemoryDb();
        using var context = db.CreateContext();
        var clock = new FixedClock(new DateTime(2024, 11, 01, 12, 0, 0, DateTimeKind.Utc));
        var repository = new FxRateCacheRepository(context, clock);

        var currencyKey = "Australia-Dollar";
        
        // Insert multiple entries with different dates
        context.FxRateCache.Add(new FxRateCacheEntry(currencyKey, new DateOnly(2024, 06, 30), 1.5m, clock.UtcNow));
        context.FxRateCache.Add(new FxRateCacheEntry(currencyKey, new DateOnly(2024, 09, 30), 1.612m, clock.UtcNow));
        context.FxRateCache.Add(new FxRateCacheEntry(currencyKey, new DateOnly(2024, 12, 31), 1.7m, clock.UtcNow));
        await context.SaveChangesAsync();

        var anchorDate = new DateOnly(2024, 11, 01);

        // Act - should select 2024-09-30 as it's the latest <= anchorDate
        var result = await repository.GetLatestRateAsync(currencyKey, anchorDate, monthsBack: 6);

        // Assert
        result.Should().NotBeNull();
        result!.CurrencyKey.Should().Be(currencyKey);
        result.RecordDate.Should().Be(new DateOnly(2024, 09, 30), 
            "should select the latest record_date that is <= anchor date");
        result.ExchangeRate.Should().Be(1.612m);
    }

    [Fact]
    public async Task GetLatestRateAsync_InclusiveBoundary_ReturnsExactSixMonthPriorDate()
    {
        // Arrange
        using var db = new SqliteInMemoryDb();
        using var context = db.CreateContext();
        var clock = new FixedClock(new DateTime(2024, 12, 31, 12, 0, 0, DateTimeKind.Utc));
        var repository = new FxRateCacheRepository(context, clock);

        var currencyKey = "Test-Currency";
        var anchorDate = new DateOnly(2024, 12, 31);
        var exactSixMonthsPrior = new DateOnly(2024, 06, 30); // Exactly 6 months prior

        // Insert entry at exactly the 6-month boundary
        context.FxRateCache.Add(new FxRateCacheEntry(currencyKey, exactSixMonthsPrior, 1.25m, clock.UtcNow));
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetLatestRateAsync(currencyKey, anchorDate, monthsBack: 6);

        // Assert - Should include the boundary date (inclusive)
        result.Should().NotBeNull();
        result!.RecordDate.Should().Be(exactSixMonthsPrior, 
            "6-month window should be inclusive at the boundary");
        result.ExchangeRate.Should().Be(1.25m);
    }

    [Fact]
    public async Task GetLatestRateAsync_OneDayBeforeSixMonths_ReturnsNull()
    {
        // Arrange
        using var db = new SqliteInMemoryDb();
        using var context = db.CreateContext();
        var clock = new FixedClock(new DateTime(2024, 12, 31, 12, 0, 0, DateTimeKind.Utc));
        var repository = new FxRateCacheRepository(context, clock);

        var currencyKey = "Test-Currency";
        var anchorDate = new DateOnly(2024, 12, 31);
        var oneDayBeforeSixMonths = new DateOnly(2024, 06, 29); // One day before 6-month boundary

        // Insert entry one day before the 6-month window
        context.FxRateCache.Add(new FxRateCacheEntry(currencyKey, oneDayBeforeSixMonths, 1.25m, clock.UtcNow));
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetLatestRateAsync(currencyKey, anchorDate, monthsBack: 6);

        // Assert - Should NOT find any record (outside window)
        result.Should().BeNull("entry is outside the 6-month window");
    }

    [Fact]
    public async Task GetLatestRateAsync_NoEntriesInWindow_ReturnsNull()
    {
        // Arrange
        using var db = new SqliteInMemoryDb();
        using var context = db.CreateContext();
        var clock = new FixedClock(new DateTime(2024, 11, 01, 12, 0, 0, DateTimeKind.Utc));
        var repository = new FxRateCacheRepository(context, clock);

        var currencyKey = "NonExistent-Currency";
        var anchorDate = new DateOnly(2024, 11, 01);

        // Act - no entries exist for this currency
        var result = await repository.GetLatestRateAsync(currencyKey, anchorDate);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestRateAsync_FiltersByCurrencyKey()
    {
        // Arrange
        using var db = new SqliteInMemoryDb();
        using var context = db.CreateContext();
        var clock = new FixedClock(new DateTime(2024, 11, 01, 12, 0, 0, DateTimeKind.Utc));
        var repository = new FxRateCacheRepository(context, clock);

        // Insert entries for different currencies
        context.FxRateCache.Add(new FxRateCacheEntry("Australia-Dollar", new DateOnly(2024, 09, 30), 1.612m, clock.UtcNow));
        context.FxRateCache.Add(new FxRateCacheEntry("Euro-Zone-Euro", new DateOnly(2024, 09, 30), 0.934m, clock.UtcNow));
        await context.SaveChangesAsync();

        var anchorDate = new DateOnly(2024, 11, 01);

        // Act
        var result = await repository.GetLatestRateAsync("Australia-Dollar", anchorDate);

        // Assert - Should only return the Australia-Dollar entry
        result.Should().NotBeNull();
        result!.CurrencyKey.Should().Be("Australia-Dollar");
        result.ExchangeRate.Should().Be(1.612m);
    }

    [Fact]
    public async Task GetLatestRateAsync_MultipleEntriesForSameCurrency_ReturnsLatest()
    {
        // Arrange
        using var db = new SqliteInMemoryDb();
        using var context = db.CreateContext();
        var clock = new FixedClock(new DateTime(2024, 11, 01, 12, 0, 0, DateTimeKind.Utc));
        var repository = new FxRateCacheRepository(context, clock);

        var currencyKey = "Test-Currency";
        
        // Insert multiple entries in non-chronological order
        context.FxRateCache.Add(new FxRateCacheEntry(currencyKey, new DateOnly(2024, 07, 15), 1.3m, clock.UtcNow));
        context.FxRateCache.Add(new FxRateCacheEntry(currencyKey, new DateOnly(2024, 10, 15), 1.6m, clock.UtcNow));
        context.FxRateCache.Add(new FxRateCacheEntry(currencyKey, new DateOnly(2024, 08, 15), 1.4m, clock.UtcNow));
        await context.SaveChangesAsync();

        var anchorDate = new DateOnly(2024, 11, 01);

        // Act
        var result = await repository.GetLatestRateAsync(currencyKey, anchorDate, monthsBack: 6);

        // Assert - Should return 2024-10-15 as it's the latest within window
        result.Should().NotBeNull();
        result!.RecordDate.Should().Be(new DateOnly(2024, 10, 15));
        result.ExchangeRate.Should().Be(1.6m);
    }
}
