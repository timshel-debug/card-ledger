using CardService.Application.Common;
using CardService.Application.Ports;
using CardService.Application.Services;
using CardService.Infrastructure.ExternalServices;
using CardService.Infrastructure.Persistence;
using CardService.Infrastructure.Repositories;
using CardService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace CardService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetValue<string>("DB__ConnectionString") ?? "Data Source=App_Data/app.db";
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        // Repositories
        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<IFxRateCache, FxRateCacheRepository>();

        // Services
        services.AddSingleton<IClock, SystemClock>();
        
        var cardHashSalt = configuration.GetValue<string>("CARD__HashSalt") ?? "default-salt-change-in-production";
        services.AddSingleton<ICardNumberHasher>(new CardNumberHasher(cardHashSalt));

        // Treasury FX Provider with HttpClient and Polly
        var treasuryBaseUrl = configuration.GetValue<string>("FX__BaseUrl") ?? "https://api.fiscaldata.treasury.gov/services/api/fiscal_service/";
        var timeoutSeconds = configuration.GetValue<int>("FX__TimeoutSeconds", 2);
        var retryCount = configuration.GetValue<int>("FX__RetryCount", 2);

        services.AddHttpClient<ITreasuryFxRateProvider, TreasuryFxRateProvider>(client =>
        {
            client.BaseAddress = new Uri(treasuryBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds + 5); // Add buffer for retries
        })
        .AddPolicyHandler(GetRetryPolicy(retryCount))
        .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(timeoutSeconds)));

        // FX Rate Resolver
        services.AddScoped<FxRateResolver>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int retryCount)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100))
            );
    }
}
