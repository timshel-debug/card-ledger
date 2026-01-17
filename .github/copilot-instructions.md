# GitHub Copilot Instructions — Card Service (WEX Implementation)

## Project Overview
Production-ready card and purchase transaction service with foreign exchange conversion using the U.S. Treasury Reporting Rates of Exchange API. SQLite-based, zero external database installation, fully tested.

## Architecture — Clean/Hexagonal
The codebase follows **Clean Architecture** (hexagonal) with strict dependency inversion:

```
Domain ← Application ← Infrastructure
   ↑                        ↑
   └────── API ─────────────┘
```

**Layer responsibilities:**
- **Domain** (`CardService.Domain`): Aggregates (`Card`), entities (`PurchaseTransaction`), value objects (`Money`, `CardNumber`, `FxRate`). Zero framework dependencies.
- **Application** (`CardService.Application`): Use cases (`CreateCardUseCase`, `CreatePurchaseUseCase`, `GetPurchaseConvertedUseCase`, `GetAvailableBalanceUseCase`), ports (interfaces), DTO mapping, validation exceptions.
- **Infrastructure** (`CardService.Infrastructure`): EF Core repositories, SQLite persistence, Treasury API client (`TreasuryFxRateProvider`), FX rate caching, resilience policies (Polly), `DependencyInjection.cs` extension method.
- **API** (`CardService.Api`): ASP.NET Core Minimal API endpoints (`CardEndpoints`, `PurchaseEndpoints`), `ExceptionHandlingMiddleware`, Problem Details error mapping, health checks, Swagger.

**Critical dependency rules**: 
- Use cases take repositories/services via constructor injection (ports defined in Application layer)
- Infrastructure implements ports (e.g., `ICardRepository`, `ITreasuryFxRateProvider`, `IClock`)
- Domain layer NEVER references EF Core, ASP.NET, or any framework types
- All DI registration happens in `Infrastructure/DependencyInjection.cs::AddInfrastructure()` and `Program.cs`

## Data Model & Storage
**SQLite file-based** (satisfies "no external DB" requirement). EF Core with code-first migrations.

### Key tables & mapping patterns
- **`cards`**: `id` (GUID), `card_number_hash` (SHA-256, UNIQUE index), `last4`, `credit_limit_cents` (INTEGER), `created_utc`
- **`purchases`**: `id` (GUID), `card_id` (FK to cards), `description` (max 50 chars), `transaction_date` (ISO date), `amount_cents` (INTEGER)
- **`fx_rate_cache`**: composite PK `(currency_key, record_date)`, `exchange_rate` (decimal), `cached_utc`

**EF Core configuration pattern**: Use `IEntityTypeConfiguration<T>` in `Infrastructure/Persistence/Configurations/` (e.g., `CardConfiguration.cs`) to keep mapping separate from entities. Convention: snake_case column names (`card_number_hash`), table names lowercase.

**Money handling**: 
- Store USD as **integer cents** (`long`) to avoid floating-point errors
- `Money.FromUsd(decimal)` converts dollars → cents (rounded)
- `Money.ToDecimal()` converts cents → dollars for display
- FX conversion: `convertedAmount = Round(usdDecimal * rate, 2, MidpointRounding.AwayFromZero)`
- **Never** use `float` or `double` for money

**Security**: 
- Card numbers MUST be stored as `SHA-256(cardNumber)` + `last4` only via `ICardNumberHasher` (production requires `CARD__HashSalt` config)
- Never log plaintext card numbers
- Repository checks uniqueness via `CardNumberHash` index

## Currency Conversion Rules (Critical Business Logic)
See [requirements.md](../requirements.md#requirement-3) and [01-solution-design.md](../solution-design-bundle/docs/01-solution-design.md).

**Window constraint**: Use exchange rate with `record_date <= anchorDate` **and** within **6 months** prior (calendar months, inclusive boundary at exactly 6 months).

**Selection algorithm**: Select the **latest rate** (max `record_date`) within the valid window.

**Cache-first strategy**:
1. Query local `fx_rate_cache` for valid rate
2. On cache miss: call Treasury API (`rates_of_exchange` endpoint)
3. On upstream failure: serve from cache if available; otherwise return 503 `FX-5030`
4. On no rate found: return 422 `FX-4220` (conversion unavailable)

**Anchor dates**:
- Purchase conversion: `anchorDate = purchase.TransactionDate`
- Balance conversion: `anchorDate = asOfDate ?? currentUtcDate`

**Currency key format**: Use Treasury's `country_currency_desc` (e.g., `Australia-Dollar`, `Austria-Euro`) for deterministic, unambiguous currency selection.

## API Contract
See [03-api-contract-openapi.yaml](../solution-design-bundle/docs/03-api-contract-openapi.yaml).

**Endpoints**:
- `POST /cards` — Create card (returns 201 + `cardId`)
- `POST /cards/{cardId}/purchases` — Create purchase (returns 201 + `purchaseId`)
- `GET /cards/{cardId}/purchases/{purchaseId}?currencyKey={key}` — Get purchase converted to target currency
- `GET /cards/{cardId}/balance?currencyKey={key}&asOfDate={date}` — Get available balance (credit limit - purchases), optionally converted

**Error codes** (use RFC 7807 Problem Details with `code` extension):
- `VAL-0001` — Validation failure (400)
- `RES-4040` — Resource not found (404)
- `DB-4090` — Duplicate card number (409)
- `FX-4220` — Conversion unavailable, no rate in 6-month window (422)
- `FX-5030` — FX upstream unavailable and no cached fallback (503)

## Development Workflow

### Build & Test
```powershell
# Full solution build (uses Central Package Management)
dotnet restore
dotnet build

# Run all tests (unit + integration)
dotnet test

# Run specific test project
dotnet test tests/CardService.Api.Tests
```

### Run locally
```powershell
# Run API (auto-migrates DB by default in non-Production environments)
dotnet run --project src/CardService.Api

# Swagger available at: http://localhost:5000/swagger (if OpenApi__Enabled=true)
# Health checks: /health/live, /health/ready
```

**Key environment variables** (PowerShell):
```powershell
$Env:ASPNETCORE_ENVIRONMENT = "Development"
$Env:CARD__HashSalt = "dev-only-salt"              # Required in production
$Env:DB__ConnectionString = "Data Source=App_Data/app.db"
$Env:OpenApi__Enabled = "true"
# Note: DB__AutoMigrate defaults to true in non-Production; set to "false" to disable
```

### Migrations
EF Core migrations (uses `AppDbContextFactory` for design-time tooling):
```powershell
# Add new migration
dotnet ef migrations add <MigrationName> --project src/CardService.Infrastructure --startup-project src/CardService.Api

# Apply migrations manually
dotnet ef database update --project src/CardService.Infrastructure --startup-project src/CardService.Api

# Generate SQL script
dotnet ef migrations script --project src/CardService.Infrastructure --startup-project src/CardService.Api
```

**Migration best practices**:
- Auto-migrate is ON by default in Development, TestNet, and Staging; no configuration needed
- In Production, auto-migrate is OFF by default; migrations must be applied explicitly via CI/CD pipeline
- To disable auto-migrate in non-Production: set `DB__AutoMigrate=false`
- Always test migrations with seed data before deployment

## Testing Strategy
See [05-testing-validation.md](solution-design/docs/05-testing-validation.md).

**Unit tests** (`CardService.Domain.Tests`, fast):
- Domain: `CardNumber` 16-digit validation, `Description` ≤50 chars, money rounding, FX window logic
- FX: "latest rate ≤ date" selection, 6-month window calculation, conversion rounding
- Run with: `dotnet test tests/CardService.Domain.Tests`

**Integration tests** (`CardService.Api.Tests`, medium):
- Use `TestWebApplicationFactory` + shared in-memory SQLite (`Data Source=:memory:;Cache=Shared`)
- `FakeTreasuryFxRateProvider` for deterministic FX data (no HTTP calls)
- `FixedClock` (default: 2024-12-31 12:00 UTC) for time-dependent tests
- Database cleared between tests via `ClearDatabase()` helper
- Test persistence constraints (card number uniqueness → 409, FK integrity)
- End-to-end API flows (POST card → POST purchase → GET converted)

**Test base class pattern** (see `IntegrationTestBase.cs`):
```csharp
public class MyIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task Test_Scenario()
    {
        // Arrange: seed data, configure FakeTreasuryProvider
        Factory.FakeTreasuryProvider.AddRate(currencyKey, recordDate, rate);
        
        // Act: call API via HttpClient
        var response = await Client.PostAsJsonAsync("/cards", request);
        
        // Assert: status code + response DTO
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
```

**Key acceptance criteria**:
- **AC-03**: Conversion returns correct rate (≤ purchase date, within 6 months, rounded to 2 decimals)
- **Window boundary**: Purchase on 2024-12-31, rate at 2024-06-30 → should succeed (exactly 6 months inclusive)
- **Latest rate**: Rates at 2024-09-30 and 2024-12-31, purchase 2024-11-01 → select 2024-09-30
- **Cache fallback**: Pre-seed cache, simulate Treasury timeout → conversion succeeds via cache

## Resilience & Error Handling
Use **Polly** for Treasury API calls:
- **Timeout**: 2 seconds per request
- **Retry**: 2 attempts with exponential backoff + jitter
- **Circuit breaker**: Open after 5 consecutive failures, half-open retry after 30s

On upstream failure:
1. Attempt cache fallback
2. If cache has valid rate → return 200
3. Otherwise → return 503 with `FX-5030`

## Configuration
Environment variables (see [06-deployment-rollback.md](../solution-design-bundle/docs/06-deployment-rollback.md)):
- `DB__ConnectionString` — SQLite file path
- `FX__BaseUrl` — Treasury Fiscal Data API base URL
- `FX__TimeoutSeconds` / `FX__RetryCount` / `FX__CircuitBreakerFailures`
- `CARD__HashSalt` — Required in production for card number hashing

## Key Files & Patterns
- **Domain invariants**: Enforce in aggregate constructors (e.g., `Card.Create()` validates 16-digit card number)
- **Value objects**: `Money` encapsulates amount + currency, enforces rounding rules
- **Repository pattern**: Infrastructure implements `ICardRepository`, `IPurchaseRepository`, `IFxRateCache` ports
- **Use case orchestration**: Application layer coordinates domain + infrastructure via injected ports
- **Minimal API endpoints**: Static classes in `Api/Endpoints/` use extension methods (`MapCardEndpoints()`, `MapPurchaseEndpoints()`)
- **DI registration**: `AddInfrastructure(config, env)` in `Infrastructure/DependencyInjection.cs` centralizes all infrastructure setup
- **EF Core configs**: Separate `IEntityTypeConfiguration<T>` classes in `Persistence/Configurations/` (not in DbContext)
- **Central Package Management**: `Directory.Packages.props` defines all package versions (no versions in .csproj files)

## Common Pitfalls to Avoid
1. **Do NOT** use floating-point for money — always integer cents in storage, decimal in application layer
2. **Do NOT** store plaintext card numbers — hash with SHA-256, store only hash + last4
3. **FX window**: Must enforce both `<=` date AND 6-month window; missing either causes incorrect behavior
4. **Rounding**: Use `MidpointRounding.AwayFromZero` for currency conversion (not banker's rounding)
5. **Error responses**: Always use Problem Details with `code` field for client-side error handling
6. **Configuration keys**: Support both `__` (double underscore) and `:` (colon) separators for env vars (e.g., `CARD__HashSalt` or `CARD:HashSalt`)
7. **Test isolation**: Call `Factory.ClearDatabase()` between integration tests to avoid data pollution
8. **EF Core migrations**: Always specify both `--project` (Infrastructure) and `--startup-project` (Api) for design-time tooling

## Codebase Conventions
- **Naming**: snake_case for DB columns/tables, PascalCase for C# types/properties
- **Project references**: Domain → nothing; Application → Domain; Infrastructure → Application + Domain; Api → all layers
- **Async all the way**: All use case methods return `Task<T>`, repositories use `CancellationToken`
- **DTO mapping**: Manual mapping in use cases (no AutoMapper), DTOs live in `Application/DTOs/`
- **Exception handling**: Throw domain exceptions (e.g., `ValidationException`, `DuplicateResourceException`) from use cases; `ExceptionHandlingMiddleware` maps to HTTP status codes
- **Assertions**: Use `Shouldly` (AwesomeAssertions) in tests: `response.StatusCode.ShouldBe(HttpStatusCode.OK)`

## Next Steps for Implementation
The design docs are complete. Implementation priority:
1. Project structure (Domain/Application/Infrastructure/API/Tests)
2. Domain model + EF Core mappings + migrations
3. Card + Purchase create endpoints with validation
4. Treasury API client + FX cache + resilience
5. Conversion endpoints (purchase + balance)
6. Integration tests with `WebApplicationFactory`

**Current Status**: ✅ Fully implemented and tested. All requirements met. See test reports in root directory.
