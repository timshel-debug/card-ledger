# GitHub Copilot Instructions — Card Service (WEX Implementation)

## Project Overview
This is a **production-ready card and purchase transaction service** with foreign exchange conversion using the U.S. Treasury Reporting Rates of Exchange API. The implementation must meet specific requirements (see [requirements.md](../requirements.md)) including zero external database installation and automated functional testing.

## Architecture — Clean/Hexagonal
The codebase follows **Clean Architecture** (hexagonal) with strict dependency inversion:

```
Domain ← Application ← Infrastructure
   ↑                        ↑
   └────── API ─────────────┘
```

**Layer responsibilities:**
- **Domain**: Aggregates (`Card`), entities (`PurchaseTransaction`), value objects (`Money`, `CardNumber`, `FxRate`). Zero framework dependencies.
- **Application**: Use cases (`CreateCard`, `CreatePurchase`, `GetPurchaseConverted`, `GetAvailableBalance`), ports (interfaces), DTO mapping.
- **Infrastructure**: EF Core repositories, SQLite persistence, Treasury API client, FX rate caching, resilience policies (Polly).
- **API**: ASP.NET Core Minimal API endpoints, request validation, Problem Details error mapping, health checks.

**Critical**: High-level policies (use cases) depend on abstractions only. Infrastructure details are injected via DI.

## Data Model & Storage
**SQLite file-based** (satisfies "no external DB" requirement). See [04-data-model.md](../solution-design-bundle/docs/04-data-model.md).

### Key tables
- `cards`: `id` (GUID), `card_number_hash` (SHA-256, UNIQUE), `last4`, `credit_limit_cents` (INTEGER)
- `purchases`: `id` (GUID), `card_id` (FK), `description` (max 50 chars), `transaction_date` (ISO date), `amount_cents` (INTEGER)
- `fx_rate_cache`: composite PK `(currency_key, record_date)`, `exchange_rate`, `cached_utc`

**Money handling**: Store USD as **integer cents** to avoid floating-point errors. Convert using decimal arithmetic: `convertedAmount = Round(usdDecimal * rate, 2, MidpointRounding.AwayFromZero)`.

**Security**: Card numbers MUST be stored as `SHA-256(cardNumber)` + `last4` only. Never persist plaintext card numbers.

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
dotnet restore
dotnet build
dotnet test
```

### Run locally
```powershell
dotnet run --project <API-project-path>
# SQLite DB auto-created at app startup with migrations applied
# Default: http://localhost:5000
```

### Migrations
Use EF Core migrations:
```powershell
dotnet ef migrations add <MigrationName> --project <Infrastructure-project>
dotnet ef database update --project <API-project>
```
Migrations auto-apply in dev; run explicitly in CI/CD for prod.

## Testing Strategy
See [05-testing-validation.md](../solution-design-bundle/docs/05-testing-validation.md).

**Unit tests** (fast):
- Domain: `CardNumber` 16-digit validation, `Description` ≤50 chars, money rounding, FX window logic
- FX: "latest rate ≤ date" selection, 6-month window calculation, conversion rounding

**Integration tests** (medium):
- Use `WebApplicationFactory` + in-memory/temp SQLite
- Test persistence constraints (card number uniqueness → 409, FK integrity)
- End-to-end API flows with mocked Treasury HTTP client

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

## Common Pitfalls to Avoid
1. **Do NOT** use floating-point for money — always integer cents in storage, decimal in application layer
2. **Do NOT** store plaintext card numbers — hash with SHA-256, store only hash + last4
3. **FX window**: Must enforce both `<=` date AND 6-month window; missing either causes incorrect behavior
4. **Rounding**: Use `MidpointRounding.AwayFromZero` for currency conversion (not banker's rounding)
5. **Error responses**: Always use Problem Details with `code` field for client-side error handling

## Next Steps for Implementation
The design docs are complete. Implementation priority:
1. Project structure (Domain/Application/Infrastructure/API/Tests)
2. Domain model + EF Core mappings + migrations
3. Card + Purchase create endpoints with validation
4. Treasury API client + FX cache + resilience
5. Conversion endpoints (purchase + balance)
6. Integration tests with `WebApplicationFactory`
