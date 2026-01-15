# Data Model and Persistence

## 1. Storage Choice
SQLite file-backed database:
- Satisfies “no separate DB install” requirement.
- Works locally and in container.
- Supports constraints, transactions, and indexing.

## 2. Tables

### 2.1 `cards`
- `id` (TEXT, PK) — GUID string
- `card_number_hash` (TEXT, NOT NULL, UNIQUE)
- `last4` (TEXT, NOT NULL)
- `credit_limit_cents` (INTEGER, NOT NULL) — positive
- `created_utc` (TEXT, NOT NULL)

**Indexes**
- Unique index on `card_number_hash`

### 2.2 `purchases`
- `id` (TEXT, PK)
- `card_id` (TEXT, NOT NULL, FK -> cards.id)
- `description` (TEXT, NOT NULL) — max 50
- `transaction_date` (TEXT, NOT NULL) — ISO date (YYYY-MM-DD)
- `amount_cents` (INTEGER, NOT NULL) — positive
- `created_utc` (TEXT, NOT NULL)

**Indexes**
- Index on `(card_id, transaction_date)`
- Index on `card_id` for aggregation

### 2.3 `fx_rate_cache`
- `currency_key` (TEXT, NOT NULL)
- `record_date` (TEXT, NOT NULL) — date
- `exchange_rate` (NUMERIC, NOT NULL) — decimal
- `cached_utc` (TEXT, NOT NULL)

**Primary Key**
- Composite PK `(currency_key, record_date)`

**Indexes**
- Index on `(currency_key, record_date DESC)`

## 3. Money Handling
- Persist USD values as integer cents.
- Parse inbound values to cents using decimal arithmetic.
- For conversion: `converted = Round(amountUsdDecimal * exchangeRate, 2, MidpointRounding.AwayFromZero)`.

## 4. Migrations
- Use EF Core migrations and auto-apply on startup (dev).
- In prod, prefer explicit migration step in CI/CD.
