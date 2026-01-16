# Detailed Solution Design — Card & Purchase Transactions with Treasury FX Conversion

## 1. Introduction / Project Overview

### 1.1 Purpose
Build a production-ready application that:
1) Persists **Cards** (card number + credit limit in USD)
2) Persists **Purchase Transactions** against Cards
3) Retrieves purchases converted to a requested currency using **U.S. Treasury “Treasury Reporting Rates of Exchange”** data
4) Retrieves a Card’s **available balance** (credit limit − purchases), optionally converted to a requested currency

### 1.2 Business Goals
- Provide a stable and testable service for storing card spending data.
- Provide deterministic, auditable currency conversion rules:
  - Use the **latest exchange rate ≤ purchase date** within the **preceding 6 months**.
  - If none exists, return an explicit conversion failure.

### 1.3 Scope
**In scope**
- Card create
- Purchase create
- Purchase retrieval in a specified currency
- Available balance retrieval in a specified currency
- Persistence without external DB install
- Automated functional tests

**Out of scope**
- Authentication/authorization requirements (not specified) — designed as pluggable
- Performance test automation (explicitly not required)
- PCI compliance certification — but we still design with security hygiene

---

## 2. Requirements Analysis

### 2.1 Functional Requirements

**FR-01 Create Card**
- Accept: CardNumber (16-digit numeric string), CreditLimitUSD (positive, rounded to cent)
- Persist Card and assign unique identifier
- Enforce unique CardNumber across the system

**FR-02 Store Purchase Transaction**
- Accept: Card identifier, Description (≤50 chars), TransactionDate (valid date), PurchaseAmountUSD (positive, rounded to cent)
- Persist and assign unique identifier

**FR-03 Retrieve Purchase Transaction in specified currency**
- Retrieve previously stored purchase
- Convert from USD to requested currency using Treasury Reporting Rates of Exchange
- Include: original amount, exchange rate used, converted amount, transaction date, identifiers

Conversion rules:
- Use rate with date **≤ purchase date**
- Rate must be within **last 6 months** relative to purchase date
- If none, return error
- Converted amount rounded to **2 decimals**

**FR-04 Retrieve Available Balance in specified currency**
- AvailableBalanceUSD = CreditLimitUSD − sum(PurchaseAmountUSD for card)
- Apply same FX rules as FR-03, anchored to an **“as-of” date**:
  - Default as-of date = request date (UTC date)
  - Optional `asOfDate` parameter supported for deterministic client usage/testing

### 2.2 Non-Functional Requirements (NFRs)

**NFR-01 Maintainability**
- SOLID adherence via layered design and DI boundaries.
- High cohesion, low coupling.
- Extensive automated tests and clear contracts.

**NFR-02 Reliability**
- Deterministic conversion selection algorithm.
- Resilience for external FX API: timeouts, retries, circuit breaker, cached fallback.
- Clear error taxonomy (validation, not found, conversion unavailable, upstream failure).

**NFR-03 Availability**
- Single-instance friendly, stateless API with backing store.
- Health endpoints for liveness/readiness.

**NFR-04 Security**
- Input validation and request size limits.
- Do not store raw card number in plaintext (see Section 6).
- TLS required in production.
- Secure secrets handling.

**NFR-05 Observability**
- Structured logs, correlation IDs.
- Metrics for request latency, error rates, FX cache hit rates, upstream availability.
- Tracing for external calls.

**NFR-06 Portability**
- Repository runs with `dotnet run` using SQLite file.
- Optional Docker image.

---

## 3. Background / Current State

Assumption: There is **no existing system**; this is greenfield. The “current state” is manual or non-existent persistence and conversion logic. Key pain points addressed by the solution:
- Lack of persisted card/purchase records with proper validation and traceability.
- Lack of deterministic exchange-rate selection logic.
- Risk of coupling business logic directly to the external FX API (testability and reliability issues).

---

## 4. Proposed Solution / Architecture Design

### 4.1 Architectural Style
**Clean Architecture / Hexagonal**:

- **Domain**: entities, value objects, domain rules (no framework dependencies)
- **Application**: use cases, orchestration, ports (interfaces), DTO mapping
- **Infrastructure**: EF Core persistence, U.S. Treasury API adapter, caching, resilience policies
- **API**: HTTP endpoints, request validation, error mapping, OpenAPI

This enforces:
- **DIP**: high-level policies (use cases) depend on abstractions.
- **SRP**: separate concerns: validation, persistence, FX retrieval, conversion.
- **OCP**: replace FX provider, storage engine, caching without changing core domain.

### 4.2 Application Architecture (Components)

**API Layer**
- CardController (minimal endpoints)
- PurchaseController
- Error middleware / problem-details mapping
- Health endpoints

**Application Layer**
- `CreateCard` use case
- `CreatePurchase` use case
- `GetPurchaseConverted` use case
- `GetAvailableBalance` use case
- Validation services

**Domain Layer**
- Aggregate root: `Card`
- Entity: `PurchaseTransaction`
- Value objects:
  - `Money` (currency + amount in minor units)
  - `CardNumber` (validated, normalized)
  - `FxRate` (rate + effective date)

**Infrastructure Layer**
- EF Core repositories + SQLite provider
- U.S. Treasury Fiscal Data client
- FX cache store (SQLite table)
- Resilience policies (timeouts/retries/circuit breaker)
- Clock abstraction for deterministic “as-of date”

### 4.3 Data Architecture

#### Persistence
SQLite database file (e.g., `app.db`) created automatically.
- Ensures “no external DB install” requirement.
- Enforces uniqueness and referential integrity with constraints.

#### Money Representation
All money stored as **integer cents** (`long`) in USD.
- Avoid floating-point rounding errors.
- Convert via decimal arithmetic in application layer.

### 4.4 Integration Architecture (Treasury FX API)

#### External API
U.S. Treasury Fiscal Data Service: **rates_of_exchange** endpoint.

Proposed request pattern:
- Query for the target currency key, with date window and descending sort by date, limit 1.
- Example filter:
  - `record_date:lte:{purchaseDate},record_date:gte:{purchaseDateMinus6Months},country_currency_desc:eq:{currencyKey}`
- Sort: `-record_date`
- Page size 1

If response empty ⇒ conversion unavailable error.

#### Currency Key
To avoid ambiguity in “Dollar” and similar currency names, the design uses the dataset’s **`country_currency_desc`** as the canonical “currency selector” (e.g., `Australia-Dollar`, `Austria-Euro`).
- The API additionally exposes a discovery endpoint `GET /currencies` (optional) to list supported keys.
- This is explicit, deterministic, and aligned to the dataset.

### 4.5 Security Architecture

- **Card number storage**: store `CardNumberHash` (SHA-256) + `Last4` + masked display string, never plaintext.
- Enforce **unique constraint** on `CardNumberHash`.
- API input validation + rate limiting (optional).
- TLS termination in production.
- Secure configuration: connection strings, any salts/keys via environment variables / secret store.

### 4.6 Technology Stack

- .NET 8
- ASP.NET Core Minimal API
- EF Core + SQLite
- FluentValidation (or built-in minimal validation) for request models
- OpenTelemetry (optional) for tracing/metrics
- Polly for resilience policies
- xUnit + FluentAssertions for tests

---

## 5. Detailed Component Design

### 5.1 Domain Model

#### 5.1.1 Card Aggregate
**Responsibilities**
- Maintain card identity + credit limit invariant
- Maintain relationship to purchases (conceptual; persistence can be separate)

**Invariants**
- CardNumber is exactly 16 digits
- CreditLimitUSD > 0
- CardNumber uniqueness enforced at persistence boundary
- AvailableBalanceUSD = CreditLimitUSD − sum(purchases)

**Key operations**
- `Create(cardNumber, creditLimitUsd)`
- `RecordPurchase(description, transactionDate, amountUsd)`

#### 5.1.2 PurchaseTransaction Entity
**Invariants**
- Description length ≤ 50
- AmountUSD > 0
- TransactionDate is a valid date

### 5.2 Application Use Cases

#### 5.2.1 CreateCard
Inputs: cardNumber, creditLimitUsd
Steps:
1) Validate input
2) Normalize card number and hash
3) Persist card
4) Return created card id

Errors:
- 400 validation error
- 409 if card already exists (unique constraint)

#### 5.2.2 CreatePurchase
Inputs: cardId, description, transactionDate, amountUsd
Steps:
1) Validate input
2) Ensure card exists
3) Persist purchase
4) Return purchase id

Errors:
- 404 card not found
- 400 validation error

#### 5.2.3 GetPurchaseConverted
Inputs: cardId, purchaseId, currencyKey
Steps:
1) Load purchase (and ensure belongs to card)
2) Resolve FX rate for (currencyKey, purchase.TransactionDate)
3) Convert:
   - `converted = round2(purchaseUsd * exchangeRate)`
4) Return purchase + rate info

Errors:
- 404 not found
- 422 conversion unavailable (no rate in window)
- 503 upstream unavailable (if cache miss and upstream failure)

#### 5.2.4 GetAvailableBalance
Inputs: cardId, currencyKey?, asOfDate?
Steps:
1) Compute balanceUsd from persisted data
2) If currencyKey provided:
   - Resolve FX rate for (currencyKey, asOfDate)
   - Convert: `balanceFx = round2(balanceUsd * exchangeRate)`
3) Return

Errors:
- 404 card not found
- 422 conversion unavailable (no rate in window)
- 503 upstream unavailable (if cache miss and upstream failure)

### 5.3 FX Rate Resolution Algorithm

**Function** `ResolveRate(currencyKey, anchorDate)`:
1) Validate `currencyKey` not null/empty
2) Determine window:
   - start = anchorDate − 6 months (calendar months)
   - end = anchorDate
3) Try local cache store:
   - Query for max(record_date) where currencyKey matches and record_date in [start, end]
4) If found: return
5) Else call Treasury API:
   - Query with filter date range and currencyKey
   - Sort descending by record_date, limit 1
   - If found: persist into cache store and return
6) Else: return conversion-unavailable

**Resilience**
- Timeout (e.g., 2s), retry with jitter (2 attempts), circuit breaker (e.g., 5 failures / 30s).
- If upstream fails and cache has any rate within window: serve cached.
- If neither: 503.

### 5.4 Data Validation Strategy
- Synchronous validation at API boundary (fail fast).
- Domain constructors enforce invariants (defensive).
- Persistence constraints enforce uniqueness (last line of defense).

### 5.5 Error Model
Use Problem Details (`application/problem+json`) with stable codes:
- `VAL-0001` validation failure
- `RES-4040` resource not found
- `FX-4220` conversion unavailable (no rate within 6 months)
- `FX-5030` FX upstream unavailable and no cached fallback
- `DB-4090` duplicate card number

---

## 6. Data Model, Storage, and Migration Strategy (summary)

See `04-data-model.md` for schema detail.

Key points:
- Store amounts in cents.
- Store card number hash for security and uniqueness.
- Maintain separate table for cached FX rates.

---

## 7. Implementation Plan

### 7.1 Phases

**Phase 0 — Repository skeleton (0.5–1 day)**
- Create solution structure (Domain/Application/Infrastructure/API/Test projects)
- CI pipeline for `dotnet test`

**Phase 1 — Core domain + persistence (1–2 days)**
- Domain model, EF Core mapping, migrations
- Card create + Purchase create endpoints + tests

**Phase 2 — FX integration + conversion (1–2 days)**
- Treasury API client + caching + resilience
- Purchase retrieval converted
- Balance endpoint converted
- Contract/integration tests (mocked upstream)

**Phase 3 — Production hardening (1–2 days)**
- Observability (logging, metrics)
- Health checks
- Final docs, OpenAPI, runbooks

### 7.2 Milestones
- M1: CRUD for cards/purchases with validations + tests
- M2: Conversion features pass all cases incl. no-rate window
- M3: Ops readiness: logs/metrics/health + rollback approach documented

---

## 8. Testing and Validation Plan (summary)

See `05-testing-validation.md` for test matrix and acceptance criteria.

---

## 9. Deployment and Rollback Plan (summary)

See `06-deployment-rollback.md`.

---

## 10. Monitoring and Alerting Plan (summary)

See `07-monitoring-alerting.md`.

---

## 11. Risks, Assumptions, Constraints (summary)

See `08-risks-assumptions-constraints.md`.

---

## 12. Cost Analysis (summary)

See `09-cost-analysis.md`.

---

## 13. Alternatives Considered (summary)

See `10-alternatives-considered.md`.

---

## 14. SOLID Application and Design Patterns

### 14.1 SOLID Mapping

**SRP**
- Domain entities: only business invariants and state transitions
- FX client: only upstream communication
- FX resolver: only rate-selection logic
- API endpoints: only request/response mapping

**OCP**
- FX provider behind interface `IFxRateProvider` — swap Treasury source without changing use cases
- Storage behind repositories — swap SQLite for another provider

**LSP**
- Ensure interface implementations (e.g., `IFxRateProvider`) behave consistently:
  - Same error semantics
  - Same rounding rules

**ISP**
- Separate ports:
  - `ICardRepository`, `IPurchaseRepository`, `IFxRateProvider`
  - Keep interfaces narrow and use-case oriented

**DIP**
- Use cases depend on interfaces, not EF Core or HttpClient directly

### 14.2 Proposed Design Patterns

- **Repository Pattern**: persistence abstraction for Card/Purchase/FxRateCache
- **Unit of Work** (via EF Core DbContext scope): transactional integrity for writes
- **Adapter Pattern**: Treasury API adapter implementing `IFxRateProvider`
- **Strategy Pattern**: pluggable rate selection (e.g., “latest <= date within window”)
- **Decorator Pattern**: caching decorator around upstream provider
- **Circuit Breaker / Retry**: resilience around external calls (Polly)
- **Factory**: `ProblemDetailsFactory` for stable error payloads
- **Specification Pattern** (optional): query specifications for “latest rate in window”
- **Options Pattern**: config for external base URL, timeout, cache TTL
