using CardService.Application.Common;
using CardService.Application.Ports;
using CardService.Domain.ValueObjects;
using CardService.Infrastructure.Persistence;
using CardService.Tests.Common.TestHelpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CardService.Api.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    public FixedClock FixedClock { get; } = new FixedClock(new DateTime(2024, 12, 31, 12, 0, 0, DateTimeKind.Utc));
    public FakeTreasuryFxRateProvider FakeTreasuryProvider { get; } = new FakeTreasuryFxRateProvider();

    public TestWebApplicationFactory()
    {
        // Create and open a single shared in-memory SQLite connection
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:;Cache=Shared");
        _connection.Open();
    }

    public void ClearDatabase()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Clear all tables in reverse order to avoid FK constraints
        db.Database.ExecuteSqlRaw("DELETE FROM purchases");
        db.Database.ExecuteSqlRaw("DELETE FROM fx_rate_cache");
        db.Database.ExecuteSqlRaw("DELETE FROM cards");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set test environment explicitly
        builder.UseEnvironment("Testing");

        // Override configuration for tests
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["DB:AutoMigrate"] = "false",
                ["CARD:HashSalt"] = "test-salt-for-integration-tests",
                ["CARD__HashSalt"] = "test-salt-for-integration-tests"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove real database
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            // Add in-memory database using the shared connection
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Replace clock with fixed clock
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(FixedClock);

            // Replace Treasury provider with fake
            services.RemoveAll<ITreasuryFxRateProvider>();
            services.AddSingleton<ITreasuryFxRateProvider>(FakeTreasuryProvider);

            // Initialize schema on the shared connection
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Dispose();
        }
        base.Dispose(disposing);
    }
}

public class FakeTreasuryFxRateProvider : ITreasuryFxRateProvider
{
    private readonly Dictionary<string, List<FxRate>> _rates = new();
    public bool ShouldThrow { get; set; }
    public Exception? ExceptionToThrow { get; set; }

    public void AddRate(string currencyKey, DateOnly recordDate, decimal exchangeRate)
    {
        if (!_rates.ContainsKey(currencyKey))
            _rates[currencyKey] = new List<FxRate>();

        _rates[currencyKey].Add(new FxRate(currencyKey, recordDate, exchangeRate));
    }

    public void Clear()
    {
        _rates.Clear();
        ShouldThrow = false;
        ExceptionToThrow = null;
    }

    public Task<FxRate?> GetLatestRateAsync(string currencyKey, DateOnly anchorDate, int monthsBack = 6, CancellationToken cancellationToken = default)
    {
        if (ShouldThrow)
            throw ExceptionToThrow ?? new HttpRequestException("Simulated upstream failure");

        if (!_rates.ContainsKey(currencyKey))
            return Task.FromResult<FxRate?>(null);

        var earliestDate = anchorDate.AddMonths(-monthsBack);
        var matchingRate = _rates[currencyKey]
            .Where(r => r.RecordDate >= earliestDate && r.RecordDate <= anchorDate)
            .OrderByDescending(r => r.RecordDate)
            .FirstOrDefault();

        return Task.FromResult(matchingRate);
    }
}
