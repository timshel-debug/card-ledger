You are implementing the solution described in the extracted “solution-design-bundle” documents. Work directly in the repo (create/edit files). Do not write narrative. Make incremental commits optional, but you must finish with: (1) `dotnet test` passing, and (2) a concise `git diff` summary of what changed.

SOURCE OF TRUTH (must follow exactly)
- docs/solution-design/01-solution-design.md
- docs/solution-design/02-architecture-diagrams.md
- docs/solution-design/03-api-contract-openapi.yaml
- docs/solution-design/04-data-model.md
- docs/solution-design/05-testing-validation.md
- docs/solution-design/06-deployment-rollback.md
- docs/solution-design/07-monitoring-alerting.md
- docs/solution-design/08-risks-assumptions-constraints.md
- docs/solution-design/09-cost-analysis.md
- docs/solution-design/10-alternatives-considered.md
- docs/solution-design/11-traceability.md

PRE-STEP (if not already done)
1) Ensure the bundle docs are present at `docs/solution-design/` (copy from the zip if needed).
2) All implementation must match the OpenAPI contract and acceptance criteria in the docs.

GOAL
Implement an industry-standard .NET 8 service that:
- Persists Cards and Purchase Transactions (SQLite file DB, EF Core, migrations)
- Retrieves converted purchase amounts and converted available balances using U.S. Treasury “rates_of_exchange”
- Uses the FX selection rule: latest rate <= anchor date within prior 6 calendar months (inclusive boundary)
- Caches FX rates locally and applies resilience for upstream calls
- Enforces validations, stable error codes via ProblemDetails, SOLID, and documented patterns
- Includes automated tests that prove the acceptance criteria

NON-NEGOTIABLE CONSTRAINTS
- Clean Architecture / Hexagonal layering (Domain, Application, Infrastructure, Api) and SOLID boundaries
- Money stored as integer cents (long) in USD; conversions via decimal arithmetic; rounding to 2 decimals away from zero
- Card number is never stored plaintext: store deterministic hash + last4; uniqueness via hash unique index
- No external DB install required (SQLite)
- Tests must not call the real Treasury API (mock HttpClient or provider)

REPO STRUCTURE (create if missing)
Create a .NET solution with:
- src/CardService.Domain
- src/CardService.Application
- src/CardService.Infrastructure
- src/CardService.Api
- tests/CardService.Domain.Tests
- tests/CardService.Api.Tests
- tests/CardService.Infrastructure.Tests (optional but recommended for Treasury client contract tests)

PROJECT DEPENDENCIES (must enforce layering)
- Domain: no references to other projects
- Application -> Domain only
- Infrastructure -> Application + Domain
- Api -> Application + Infrastructure (+ Domain allowed only for shared contracts if strictly needed, prefer DTOs in Application)

IMPLEMENTATION TASKS (do in order)

A) Solution + Common Utilities
1) Create solution + projects targeting net8.0.
2) Add standard packages (keep minimal):
   - Api: Microsoft.AspNetCore.OpenApi, Swashbuckle.AspNetCore (or NSwag), Microsoft.Extensions.Diagnostics.HealthChecks
   - Infrastructure: Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Sqlite, Microsoft.EntityFrameworkCore.Design, Polly, (optional) Microsoft.Extensions.Http.Polly
   - Tests: xUnit, FluentAssertions, Microsoft.AspNetCore.Mvc.Testing, Microsoft.NET.Test.Sdk
   - Optional for Http mocking: RichardSzalay.MockHttp OR a custom HttpMessageHandler stub
3) Add EditorConfig and enable .NET analyzers (industry standard baseline).
4) Add a deterministic clock abstraction: IClock with UtcNow / UtcToday.

B) Domain Layer (src/CardService.Domain)
Implement domain model and invariants:
1) Entities / Value Objects:
   - Card aggregate: Id (Guid), CardNumberHash (string), Last4 (string), CreditLimitCents (long), CreatedUtc
   - PurchaseTransaction: Id (Guid), CardId (Guid), Description (string <=50), TransactionDate (DateOnly), AmountCents (long), CreatedUtc
   - Value objects/utilities:
     - CardNumber parser/validator (exactly 16 digits, numeric only)
     - Money helper for parsing decimal USD input -> cents (long) and formatting cents -> decimal
     - FxRate: CurrencyKey (string), RecordDate (DateOnly), ExchangeRate (decimal)
2) Domain rules:
   - card number must be valid; credit limit > 0
   - purchase description max 50; amount > 0; valid date
3) Domain must not depend on EF Core, HttpClient, or ASP.NET.

C) Application Layer (src/CardService.Application)
Implement use cases + ports (interfaces) + DTOs:
1) Interfaces (ports) - keep narrow (ISP):
   - ICardRepository
   - IPurchaseRepository
   - IFxRateCache (or IFxRateRepository)
   - ITreasuryFxRateProvider (or IFxRateProvider)
   - IClock
2) Use cases (one class per use case):
   - CreateCard
   - CreatePurchase
   - GetPurchaseConverted
   - GetAvailableBalance
3) FX resolution service:
   - FxRateResolver with algorithm exactly as docs (cache-first, then provider, persist to cache)
   - Window is 6 calendar months inclusive; latest rate <= anchor date
4) Error taxonomy:
   - Use typed exceptions or Result model that maps cleanly to ProblemDetails:
     - VAL-0001 (validation)
     - RES-4040 (not found)
     - FX-4220 (conversion unavailable: no rate within window)
     - FX-5030 (upstream unavailable and no cached fallback)
     - DB-4090 (duplicate card)
5) DTOs:
   - Requests/responses must match docs/03-api-contract-openapi.yaml.
6) Validation:
   - Validate at API boundary, but also protect invariants in domain constructors/factories.

D) Infrastructure Layer (src/CardService.Infrastructure)
Implement persistence + FX provider + caching + resilience:
1) EF Core:
   - AppDbContext with DbSets: Cards, Purchases, FxRateCache
   - Entity configurations matching docs/04-data-model.md:
     - cards: unique card_number_hash
     - purchases: FK to cards + indexes
     - fx_rate_cache: composite PK (currency_key, record_date) + index desc
   - Migrations: create initial migration.
   - SQLite connection string configurable; default to local file (e.g., App_Data/app.db).
2) Repositories:
   - CardRepository, PurchaseRepository, FxRateCacheRepository implementing Application interfaces.
   - Ensure transactional integrity for writes (DbContext scope).
3) Card number hashing:
   - Implement CardNumberHasher with salt from configuration (CARD__HashSalt).
   - Store hash + last4; never store plaintext.
4) Treasury FX provider:
   - Typed HttpClient-based provider calling the Treasury “rates_of_exchange” endpoint.
   - Build query per docs: filter currencyKey (Treasury country_currency_desc), record_date <= anchorDate, record_date >= anchorDate - 6 months, sort desc by record_date, limit 1.
   - Parse response robustly; handle empty result -> conversion unavailable.
5) Resilience:
   - Apply timeout + retry + circuit breaker around the Treasury call.
   - If Treasury call fails AND cache has suitable rate -> serve cache.
   - If both unavailable -> FX-5030.

E) API Layer (src/CardService.Api)
Implement endpoints and operational concerns:
1) Minimal API endpoints exactly per OpenAPI:
   - POST /cards
   - POST /cards/{cardId}/purchases
   - GET /cards/{cardId}/purchases/{purchaseId}?currencyKey=...
   - GET /cards/{cardId}/balance?currencyKey=&asOfDate=
2) Input validation:
   - Return 400 + ProblemDetails + code VAL-0001 for invalid inputs.
3) Error handling middleware:
   - Map exceptions/results to ProblemDetails with the stable codes and correct HTTP statuses:
     - 400 VAL-0001
     - 404 RES-4040
     - 409 DB-4090
     - 422 FX-4220
     - 503 FX-5030
4) Health endpoints:
   - /health/live and /health/ready (ready checks DB connectivity)
5) OpenAPI/Swagger:
   - Expose Swagger UI in Development.
   - Ensure endpoint contracts align with docs/03-api-contract-openapi.yaml (names, fields, formats).
6) Observability:
   - Structured logging with correlation ID support (X-Correlation-Id passthrough or generated).
   - Log key events: create card, create purchase, FX cache hit/miss, upstream failures (no sensitive data).

F) Testing (must prove correctness)
Implement tests per docs/05-testing-validation.md. Tests must be deterministic and should not depend on current date unless explicitly controlled via IClock.

1) Domain unit tests (tests/CardService.Domain.Tests)
   - CardNumber validation (valid/invalid)
   - Description length enforcement
   - Amount parsing to cents and rounding behavior
   - Conversion rounding: MidpointRounding.AwayFromZero (2 decimals)

2) API integration tests (tests/CardService.Api.Tests)
Use WebApplicationFactory with SQLite temp file per test (or per test class) and an override for:
   - IClock (fixed date)
   - ITreasuryFxRateProvider (fake) OR HttpClient stubbed handler
Test acceptance criteria:
   - AC-01 Create Card (201, validation 400, duplicate 409)
   - AC-02 Create Purchase (201, invalid 400, missing card 404)
   - AC-03 Retrieve Purchase Converted:
     - selects latest rate <= purchase date within 6 months (inclusive boundary test)
     - no rate -> 422 FX-4220
     - upstream failure but cached rate -> 200 (prove by seeding cache and making provider throw)
     - upstream failure + no cache -> 503 FX-5030
   - AC-04 Retrieve Available Balance Converted:
     - correct USD aggregation
     - conversion uses asOfDate override and default behavior (control IClock)
     - same 422/503 behavior as purchase conversion

3) Infrastructure contract tests (tests/CardService.Infrastructure.Tests) (recommended)
   - Treasury provider builds correct query params
   - Treasury provider parses a sample payload
   - Handle empty results correctly

TEST COMMANDS (must pass)
- dotnet test

DEFINITION OF DONE (must be satisfied)
1) All endpoints implemented and match OpenAPI schema and semantics in docs/03-api-contract-openapi.yaml.
2) EF Core SQLite persistence with migrations; constraints enforced (duplicate card triggers 409 DB-4090).
3) FX resolution algorithm exactly matches docs (6-month inclusive window, latest <= date).
4) All acceptance criteria in docs/05-testing-validation.md are encoded as automated tests and passing.
5) No plaintext card numbers stored; hashing with configured salt; last4 stored.
6) Observability and health endpoints implemented as documented.

DELIVERABLES CHECKLIST (end your work by ensuring these exist)
- Solution and projects created under src/ and tests/
- EF Core migrations present
- Swagger/OpenAPI enabled in dev
- All tests passing (`dotnet test`)
- Update README or minimal run instructions (how to run API and where DB lives)

FINAL OUTPUT REQUIRED
At the end, show:
- The exact `dotnet test` output (or summary) proving green
- A concise git diff summary (files added/changed) without extra narrative
