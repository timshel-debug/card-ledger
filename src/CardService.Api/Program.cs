using CardService.Api;
using CardService.Api.Endpoints;
using CardService.Api.Middleware;
using CardService.Application.Services;
using CardService.Application.UseCases;
using CardService.Infrastructure;
using CardService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Entry point and configuration for the Card Service API application.
/// </summary>
/// <remarks>
/// <para>
/// This Program.cs file orchestrates the entire application configuration, including:
/// <list type="bullet">
/// <item><strong>Service Registration:</strong> DI container setup for all layers</item>
/// <item><strong>Database Migration:</strong> Automatic EF Core migrations on startup (dev mode)</item>
/// <item><strong>Middleware Pipeline:</strong> Exception handling, Swagger, health checks</item>
/// <item><strong>API Endpoint Mapping:</strong> Registration of all minimal API endpoints</item>
/// </list>
/// </para>
/// <para>
/// The application follows a layered architecture with strict dependency inversion:
/// <list type="bullet">
/// <item><strong>Domain:</strong> Core business entities and rules (no dependencies)</item>
/// <item><strong>Application:</strong> Use cases and ports/interfaces (no framework dependencies)</item>
/// <item><strong>Infrastructure:</strong> EF Core, HTTP clients, external services</item>
/// <item><strong>API:</strong> ASP.NET Core endpoints and middleware</item>
/// </list>
/// </para>
/// </remarks>

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Infrastructure and Application
/// <summary>
/// Extension method (defined in CardService.Infrastructure/DependencyInjection.cs)
/// that registers:
/// - EF Core DbContext with SQLite
/// - Repository implementations (CardRepository, PurchaseRepository, FxRateCacheRepository)
/// - External services (TreasuryFxRateProvider, CardNumberHasher, SystemClock)
/// - Polly resilience policies (timeout, retry, circuit breaker) for HTTP calls
/// - FX rate cache and FxRateResolver service
/// </summary>
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

// Add Use Cases
/// <summary>
/// Use cases are registered as scoped dependencies. Each HTTP request gets its own
/// instance, ensuring proper unit-of-work semantics and isolation.
/// </summary>
builder.Services.AddScoped<CreateCardUseCase>();
builder.Services.AddScoped<CreatePurchaseUseCase>();
builder.Services.AddScoped<GetPurchaseConvertedUseCase>();
builder.Services.AddScoped<GetAvailableBalanceUseCase>();

// Add Health Checks
/// <summary>
/// Health check endpoints for liveness and readiness probes.
/// Includes DbContext check to verify database connectivity.
/// </summary>
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// Add Problem Details
/// <summary>
/// Built-in ASP.NET Core problem details middleware for RFC 7807 compliance.
/// Custom exception handling is done via ExceptionHandlingMiddleware for fine-grained control.
/// </summary>
builder.Services.AddProblemDetails();

var app = builder.Build();

// Bootstrap SQLite - ensure directory exists for file-based databases
var connectionString = builder.Configuration.GetValue<string>("DB:ConnectionString")
    ?? "Data Source=App_Data/app.db";
SqliteBootstrapper.EnsureDirectoryExists(connectionString, app.Environment.ContentRootPath);

// Apply migrations on startup - defaults to true in non-Production environments, opt-in for Production
/// <summary>
/// EF Core migrations are applied automatically in non-Production environments (Development,
/// TestNet, Staging, etc.) unless explicitly disabled. In Production, migrations should be
/// applied explicitly during the deployment pipeline (opt-in via configuration).
/// </summary>
var autoMigrate = builder.Configuration.GetValue<bool?>("DB:AutoMigrate") ?? !app.Environment.IsProduction();
if (autoMigrate)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

// Configure the HTTP request pipeline
/// <summary>
/// Exception handling middleware intercepts all exceptions and converts them
/// to Problem Details (RFC 7807) responses with appropriate HTTP status codes and error codes.
/// </summary>
app.UseMiddleware<ExceptionHandlingMiddleware>();

/// <summary>
/// Swagger UI is enabled in development/staging or when OpenApi:Enabled is set to true.
/// Configurable via OpenApi:Enabled configuration key (default: true in Development).
/// </summary>
var openApiEnabled = builder.Configuration.GetValue("OpenApi:Enabled", app.Environment.IsDevelopment());
if (openApiEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Health endpoints
/// <summary>
/// Health check endpoints for container orchestration (Kubernetes, Docker Compose, etc.)
/// - /health/live — Liveness probe (is the app running?)
/// - /health/ready — Readiness probe (is the app ready to serve traffic?)
/// </summary>
app.MapHealthChecks("/health/live").WithTags("Health");
app.MapHealthChecks("/health/ready").WithTags("Health");

// API endpoints
/// <summary>
/// Maps all card and purchase endpoints defined in CardEndpoints and PurchaseEndpoints.
/// </summary>
app.MapCardEndpoints();
app.MapPurchaseEndpoints();

app.Run();

// Make Program accessible to tests
/// <summary>
/// This partial class declaration allows integration tests to reference the Program class
/// for WebApplicationFactory&lt;Program&gt; setup.
/// </summary>
public partial class Program { }
