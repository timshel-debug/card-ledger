# Testing and Validation Plan

## 1. Test Pyramid

### Unit Tests (fast)
- Domain:
  - CardNumber validation (16 digits)
  - Description length enforcement
  - Money rounding to cents
- FX:
  - Window calculation (6 months)
  - “latest <= date” selection logic
  - Conversion rounding

### Integration Tests (medium)
- API endpoints using `WebApplicationFactory` and SQLite in-memory (or temp file)
- Persistence constraints:
  - CardNumber uniqueness => 409
  - FK integrity for purchases

### Contract Tests (medium)
- Treasury client request formation (query params, filters, sorting) using mocked HttpClient.

## 2. Core Acceptance Criteria (mapped to requirements)

### AC-01 Create Card
- Given valid card number and credit limit, returns 201 and new card id.
- Given invalid card number (non-numeric or not 16 digits), returns 400 with `VAL-0001`.
- Given credit limit <= 0, returns 400.
- Given duplicate card number, returns 409 with `DB-4090`.

### AC-02 Create Purchase
- Given existing card and valid purchase, returns 201 and purchase id.
- Given description > 50, returns 400.
- Given amount <= 0, returns 400.
- Given missing card, returns 404.

### AC-03 Retrieve Purchase Converted
- Given existing purchase and currencyKey with available rate, returns:
  - original USD amount
  - exchange rate
  - converted amount rounded to 2 decimals
  - rate record_date <= purchase date and within 6 months
- Given no rate within 6 months <= purchase date, returns 422 with `FX-4220`.
- Given upstream failure but cache has suitable rate, returns 200 using cache.
- Given upstream failure and no cache, returns 503 with `FX-5030`.

### AC-04 Retrieve Available Balance Converted
- Given card with purchases, returns available balance = credit limit − sum(purchases).
- When currencyKey provided, conversion uses “as-of date”:
  - default: request date
  - optional: `asOfDate` overrides
- Same `FX-4220` and `FX-5030` behavior as purchases.

## 3. Example Test Cases (high-value)

1) **Duplicate Card**
- Create card A; create card A again => 409.

2) **Conversion Window**
- Purchase date = 2024-12-31.
- Only FX rate exists at 2024-06-30 => within 6 months? (No: 6 calendar months prior is 2024-06-30; include boundary) => should be allowed if inclusive.
- Only FX rate at 2024-06-29 => should fail (outside window).

3) **Latest Rate Selection**
- Rates available at 2024-09-30 and 2024-12-31, purchase date 2024-11-01 => selects 2024-09-30.

4) **Rounding**
- USD amount 10.01, rate 1.612 => converted must be rounded to 2 decimals.

5) **Cache Fallback**
- Pre-seed cache with valid rate.
- Simulate Treasury API timeout.
- Conversion still succeeds using cache.

## 4. Definition of Done
- All ACs pass.
- `dotnet test` passes with high coverage of domain + conversion rules.
- OpenAPI contract matches implemented endpoints.
