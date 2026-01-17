# CardService

CardService is a Clean Architecture / Hexagonal .NET service that manages:
- Cards with a credit limit (stored as integer cents)
- Purchase transactions
- FX conversion for purchases and balances using the U.S. Treasury Rates of Exchange API
- Local FX rate caching for resilience

The design is deterministic, testable, and portable, using SQLite for persistence with strict layer boundaries.

---

## Solution Structure

```
src/
  CardService.Domain/          Domain model, invariants (no infra deps)
  CardService.Application/     Use cases, ports, DTOs, IFxRateResolver
  CardService.Infrastructure/  EF Core (SQLite), repositories, Treasury client, hashing, DI
  CardService.Api/             Minimal API endpoints, middleware, Swagger, health checks

tests/
  CardService.Domain.Tests/          Domain unit tests
  CardService.Api.Tests/             API integration tests (WebApplicationFactory + in-memory SQLite)
  CardService.Infrastructure.Tests/  Infra contract tests (Treasury client + cache semantics)
  CardService.Tests.Common/          Shared test helpers (e.g., FixedClock)
```

---

## Tech Stack

- .NET 10
- ASP.NET Core Minimal APIs
- EF Core + SQLite
- Polly for resilience (timeout, retry, circuit breaker)
- Swagger/OpenAPI
- xUnit + AwesomeAssertions

---

## Prerequisites

- .NET 10 SDK (`dotnet --info`)

---

## Configuration

Standard ASP.NET Core configuration sources (appsettings, environment variables, etc.).

### Required (Production)

| Key | Description |
| --- | --- |
| `CARD__HashSalt` | Required in Production; used for deterministic hashing. Startup fails if missing. |

### Common

| Key | Description | Default |
| --- | --- | --- |
| `DB__ConnectionString` or `DB:ConnectionString` | SQLite connection string | `Data Source=App_Data/app.db` |
| `DB__AutoMigrate` or `DB:AutoMigrate` | Run EF migrations automatically on startup | `true` in non-Production; set to `false` to disable |
| `OpenApi__Enabled` or `OpenApi:Enabled` | Enable Swagger/OpenAPI endpoints | `true` in Development |

### FX / Treasury Resilience

| Key | Description | Typical |
| --- | --- | --- |
| `FX__TimeoutSeconds` | Per-request timeout | `2`–`5` |
| `FX__RetryCount` | Retry attempts | `2`–`3` |
| `FX__CircuitBreakerFailures` | Failures before circuit opens | `5` |
| `FX__CircuitBreakerDurationSeconds` | Break duration (seconds) | `30` |

---

## Running Locally

```bash
dotnet run --project src/CardService.Api/CardService.Api.csproj
```

Swagger (if enabled): `https://localhost:<port>/swagger`

### Helpful env vars (PowerShell)

```powershell
$Env:ASPNETCORE_ENVIRONMENT = "Development"
$Env:CARD__HashSalt = "dev-only-salt"
$Env:OpenApi__Enabled = "true"
# Note: DB__AutoMigrate defaults to true in non-Production environments
```

---

## Database and Migrations

SQLite via EF Core.

- **Auto-migrate** (default in non-Production): Runs automatically in Development/TestNet/Staging unless disabled via `DB__AutoMigrate=false`.
- **Manual migration**: `dotnet ef database update --project src/CardService.Infrastructure --startup-project src/CardService.Api`
- **Disable auto-migrate**: Set `DB__AutoMigrate=false` in any environment.

---

## API Summary

- `POST /cards` — create card (card number hashed, plaintext never stored)
- `POST /cards/{cardId}/purchases` — record purchase
- `GET /cards/{cardId}/purchases/{purchaseId}?currencyKey=...` — purchase with optional FX conversion
- `GET /cards/{cardId}/balance?currencyKey=...&asOfDate=...` — balance with optional FX conversion

Health: `/health/live`, `/health/ready`

---

## FX Conversion Rules

- Anchor date = purchase date (or `asOfDate` for balance)
- Select latest rate where `record_date <= anchorDate` within prior 6 months (inclusive)
- Cache-first; on miss, call Treasury and cache result
- On upstream failure, use cache only if still within validity window; otherwise fail

---

## Security Notes

- Card numbers validated (16 digits) and never stored in plaintext
- Stored fields: `CardNumberHash` (salted), `Last4`
- Production requires `CARD__HashSalt` (fail-fast)
- Logs must avoid sensitive data

---

## Testing

```bash
dotnet test
```

Includes domain unit tests, API integration tests (WebApplicationFactory + in-memory SQLite + fake Treasury), and infrastructure contract tests (Treasury client + cache semantics).

---

## Documentation

- `docs/solution-design/` — design, API contract, testing plan
- `docs/diagrams/` — C4, sequences, ERD, error mapping, FX activity flow
- Start at `docs/diagrams/00-index.md`

---

## Troubleshooting

### `/swagger` returns 404
- Ensure Swagger is enabled (`OpenApi__Enabled=true` / `OpenApi:Enabled=true`).
- Run the API project: `dotnet run --project src/CardService.Api/CardService.Api.csproj`.
- For env-gated Swagger, set `ASPNETCORE_ENVIRONMENT=Development`.

### “no such table” errors in tests
- API tests rely on shared in-memory SQLite and schema init in `TestWebApplicationFactory`.
- Ensure DbContext uses the same open connection across requests in tests.

---

## License

Provided as-is for the purposes of the challenge/exercise.


If you want a shorter “engineering interview submission” version with explicit design decisions/tradeoffs, say so and I’ll trim accordingly.
