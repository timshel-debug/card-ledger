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
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Extensions.Http;

namespace CardService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
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
        
        // Card Hash Salt - enforce configuration in Production
        var cardHashSalt = configuration.GetValue<string>("CARD__HashSalt") ?? configuration.GetValue<string>("CARD:HashSalt");
        if (string.IsNullOrWhiteSpace(cardHashSalt))
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "CARD__HashSalt must be configured in production. Set the environment variable or configuration key to a strong random value.");
            }
            cardHashSalt = "dev-only-salt-not-for-production";
        }
        services.AddSingleton<ICardNumberHasher>(new CardNumberHasher(cardHashSalt));

        // Treasury FX Provider with HttpClient and Polly
        var treasuryBaseUrl = configuration.GetValue<string>("FX__BaseUrl") ?? "https://api.fiscaldata.treasury.gov/services/api/fiscal_service/";
        var timeoutSeconds = configuration.GetValue<int>("FX__TimeoutSeconds", 2);
        var retryCount = configuration.GetValue<int>("FX__RetryCount", 2);
        var circuitBreakerFailures = configuration.GetValue<int>("FX__CircuitBreakerFailures", 5);
        var circuitBreakerDuration = configuration.GetValue<int>("FX__CircuitBreakerDurationSeconds", 30);

        services.AddHttpClient<ITreasuryFxRateProvider, TreasuryFxRateProvider>(client =>
        {
            client.BaseAddress = new Uri(treasuryBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds + 5); // Add buffer for retries
        })
        .AddPolicyHandler(GetRetryPolicy(retryCount))
        .AddPolicyHandler(GetCircuitBreakerPolicy(circuitBreakerFailures, circuitBreakerDuration))
        .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(timeoutSeconds)));

        // FX Rate Resolver
        services.AddScoped<IFxRateResolver, FxRateResolver>();

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

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(int failureThreshold, int durationSeconds)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: failureThreshold,
                durationOfBreak: TimeSpan.FromSeconds(durationSeconds)
            );
    }
}
