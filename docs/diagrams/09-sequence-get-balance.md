# Sequence Diagram — Get Available Balance

## Purpose
Shows the flow for `GET /cards/{cardId}/balance?currencyKey=Australia-Dollar&asOfDate=2024-12-31` with optional currency conversion.

## API Endpoint
```
GET /cards/3fa85f64-5717-4562-b3fc-2c963f66afa6/balance?currencyKey=Australia-Dollar&asOfDate=2024-12-31
```

## Happy Path — USD Balance (No Conversion)

```mermaid
sequenceDiagram
    actor Client
    participant API as CardEndpoints
    participant UC as GetAvailableBalanceUseCase
    participant CardRepo as ICardRepository
    participant PurchaseRepo as IPurchaseRepository
    participant DB as SQLite

    Client->>+API: GET /cards/{cardId}/balance<br/>(no currencyKey)
    API->>+UC: ExecuteAsync(cardId, currencyKey: null, asOfDate: null)
    
    UC->>+CardRepo: GetByIdAsync(cardId)
    CardRepo->>+DB: SELECT FROM cards WHERE id = ?
    DB-->>-CardRepo: Card row (credit_limit_cents = 100000)
    CardRepo-->>-UC: Card entity
    
    UC->>+PurchaseRepo: GetByCardIdAsync(cardId)
    PurchaseRepo->>+DB: SELECT FROM purchases WHERE card_id = ?
    DB-->>-PurchaseRepo: Purchase rows
    PurchaseRepo-->>-UC: List<PurchaseTransaction>
    
    Note over UC: Calculate balance:<br/>$1000.00 - ($4.50 + $12.00 + ...)<br/>= $983.50
    
    UC-->>-API: BalanceResponse<br/>{creditLimitUsd: 1000.00, totalPurchasesUsd: 16.50, availableBalanceUsd: 983.50}
    API-->>-Client: 200 OK<br/>{...balance in USD only...}
```

## Happy Path — Balance with Conversion

```mermaid
sequenceDiagram
    actor Client
    participant API as CardEndpoints
    participant UC as GetAvailableBalanceUseCase
    participant CardRepo as ICardRepository
    participant PurchaseRepo as IPurchaseRepository
    participant FxResolver as FxRateResolver
    participant Cache as IFxRateCache
    participant DB as SQLite

    Client->>+API: GET /cards/{cardId}/balance?currencyKey=Australia-Dollar&asOfDate=2024-12-31
    API->>+UC: ExecuteAsync(cardId, "Australia-Dollar", DateOnly(2024-12-31))
    
    UC->>+CardRepo: GetByIdAsync(cardId)
    CardRepo->>DB: SELECT FROM cards...
    CardRepo-->>-UC: Card entity
    
    UC->>+PurchaseRepo: GetByCardIdAsync(cardId)
    PurchaseRepo->>DB: SELECT FROM purchases...
    PurchaseRepo-->>-UC: List<PurchaseTransaction>
    
    Note over UC: Calculate USD balance:<br/>$983.50
    
    UC->>+FxResolver: ResolveRateAsync("Australia-Dollar", DateOnly(2024-12-31))
    
    FxResolver->>+Cache: GetLatestRateAsync(...)
    Cache->>+DB: SELECT FROM fx_rate_cache<br/>WHERE currency_key = 'Australia-Dollar'<br/>AND record_date <= '2024-12-31'<br/>AND record_date >= '2024-06-30'<br/>ORDER BY record_date DESC LIMIT 1
    DB-->>-Cache: Rate row (1.612, 2024-12-30)
    Cache-->>-FxResolver: FxRate(1.612, 2024-12-30)
    
    FxResolver-->>-UC: FxRate(1.612, 2024-12-30)
    
    Note over UC: Convert balance:<br/>983.50 * 1.612 = 1585.00<br/>(rounded to 2 decimals)
    
    UC-->>-API: BalanceResponse<br/>{creditLimitUsd: 1000.00, totalPurchasesUsd: 16.50,<br/>availableBalanceUsd: 983.50, currencyKey: "Australia-Dollar",<br/>exchangeRate: 1.612, convertedAvailableBalance: 1585.00, asOfDate: "2024-12-31"}
    API-->>-Client: 200 OK<br/>{...balance in USD + converted...}
```

## AsOfDate Defaulting

```mermaid
sequenceDiagram
    actor Client
    participant API as CardEndpoints
    participant UC as GetAvailableBalanceUseCase
    participant Clock as System Clock

    Client->>+API: GET /cards/{cardId}/balance?currencyKey=Australia-Dollar<br/>(asOfDate omitted)
    API->>+UC: ExecuteAsync(cardId, "Australia-Dollar", asOfDate: null)
    
    Note over UC: asOfDate is null<br/>Default to current UTC date
    
    UC->>Clock: DateTime.UtcNow.Date
    Clock-->>UC: DateOnly(2026-01-16)
    
    Note over UC: Use 2026-01-16 as anchor date<br/>for FX rate resolution
    
    UC->>UC: (continue with balance + FX resolution...)
    
    UC-->>-API: BalanceResponse<br/>{...asOfDate: "2026-01-16"...}
    API-->>-Client: 200 OK
```

## Error Path — Card Not Found

```mermaid
sequenceDiagram
    actor Client
    participant API as CardEndpoints
    participant MW as ExceptionHandlingMiddleware
    participant UC as GetAvailableBalanceUseCase
    participant CardRepo as ICardRepository
    participant DB as SQLite

    Client->>+API: GET /cards/{invalidCardId}/balance
    API->>+MW: Next()
    MW->>+UC: ExecuteAsync(cardId, null, null)
    
    UC->>+CardRepo: GetByIdAsync(cardId)
    CardRepo->>+DB: SELECT FROM cards WHERE id = ?
    DB-->>-CardRepo: No rows
    CardRepo-->>-UC: null
    
    UC-->>MW: throw ResourceNotFoundException<br/>"Card with ID {cardId} not found"
    
    Note over MW: Map to ProblemDetails
    
    MW-->>-API: ProblemDetails response
    API-->>-Client: 404 Not Found<br/>code: RES-4040<br/>detail: "Card with ID ... not found"
```

## Error Path — FX Conversion Unavailable

```mermaid
sequenceDiagram
    actor Client
    participant API as CardEndpoints
    participant MW as ExceptionHandlingMiddleware
    participant UC as GetAvailableBalanceUseCase
    participant FxResolver as FxRateResolver

    Client->>+API: GET /cards/{cardId}/balance?currencyKey=Unknown-Currency&asOfDate=2024-12-31
    API->>+MW: Next()
    MW->>+UC: ExecuteAsync(...)
    
    Note over UC: (Card + purchases retrieved,<br/>balance calculated)
    
    UC->>+FxResolver: ResolveRateAsync("Unknown-Currency", DateOnly(2024-12-31))
    
    Note over FxResolver: (Cache miss, upstream returns no rate)
    
    FxResolver-->>UC: throw FxConversionUnavailableException<br/>"No exchange rate available..."
    
    UC-->>MW: (exception propagates)
    
    Note over MW: Map to ProblemDetails
    
    MW-->>-API: ProblemDetails response
    API-->>-Client: 422 Unprocessable Entity<br/>code: FX-4220
```

## Key Steps

1. **Card Retrieval**: Query DB for card to get credit limit
2. **Purchase Aggregation**: Sum all purchases for the card
3. **Balance Calculation**: `availableBalanceUsd = creditLimitUsd - totalPurchasesUsd`
4. **Optional Conversion**: If `currencyKey` provided:
   - Default `asOfDate` to current UTC date if omitted
   - Resolve FX rate using `FxRateResolver`
   - Convert: `convertedBalance = Round(availableBalanceUsd * exchangeRate, 2)`
5. **Response**: USD balance always included; converted balance only if `currencyKey` provided

## Query Parameters

- **currencyKey** (optional): Treasury `country_currency_desc` (e.g., `Australia-Dollar`)
- **asOfDate** (optional): Anchor date for FX rate resolution; defaults to current UTC date

## Error Codes

- **RES-4040** (404): Card not found
- **FX-4220** (422): No exchange rate available in 6-month window (only if `currencyKey` provided)
- **FX-5030** (503): Upstream unavailable and no cached fallback (only if `currencyKey` provided)
