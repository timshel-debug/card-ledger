# Sequence Diagram — Get Purchase Converted

## Purpose
Shows the flow for `GET /cards/{cardId}/purchases/{purchaseId}?currencyKey=Australia-Dollar` with FX conversion logic including cache, upstream, fallback, and error scenarios.

## API Endpoint
```
GET /cards/3fa85f64-5717-4562-b3fc-2c963f66afa6/purchases/7c9e6679-7425-40de-944b-e07fc1f90ae7?currencyKey=Australia-Dollar
```

## Happy Path — Cache Hit

```mermaid
sequenceDiagram
    actor Client
    participant API as PurchaseEndpoints
    participant UC as GetPurchaseConvertedUseCase
    participant PurchaseRepo as IPurchaseRepository
    participant FxResolver as FxRateResolver
    participant Cache as IFxRateCache
    participant DB as SQLite

    Client->>+API: GET /purchases/{id}?currencyKey=Australia-Dollar
    API->>+UC: ExecuteAsync(cardId, purchaseId, currencyKey)
    
    UC->>+PurchaseRepo: GetByIdAsync(cardId, purchaseId)
    PurchaseRepo->>+DB: SELECT FROM purchases WHERE id = ? AND card_id = ?
    DB-->>-PurchaseRepo: Purchase row
    PurchaseRepo-->>-UC: PurchaseTransaction entity
    
    UC->>+FxResolver: ResolveRateAsync(currencyKey, purchase.TransactionDate)
    
    Note over FxResolver: Compute 6-month window<br/>Start = 2024-06-20<br/>End = 2024-12-20
    
    FxResolver->>+Cache: GetLatestRateAsync(currencyKey, anchorDate, 6)
    Cache->>+DB: SELECT FROM fx_rate_cache<br/>WHERE currency_key = ? AND record_date BETWEEN ? AND ?<br/>ORDER BY record_date DESC LIMIT 1
    DB-->>-Cache: Rate row (2024-12-15, 1.612)
    Cache-->>-FxResolver: FxRate(1.612, 2024-12-15)
    
    FxResolver-->>-UC: FxRate(1.612, 2024-12-15)
    
    Note over UC: Convert: 4.50 * 1.612 = 7.25<br/>Round to 2 decimals
    
    UC-->>-API: ConvertedPurchaseResponse<br/>{amountUsd: 4.50, exchangeRate: 1.612, convertedAmount: 7.25}
    API-->>-Client: 200 OK<br/>{...converted purchase...}
```

## Cache Miss — Upstream Success

```mermaid
sequenceDiagram
    actor Client
    participant API as PurchaseEndpoints
    participant UC as GetPurchaseConvertedUseCase
    participant FxResolver as FxRateResolver
    participant Cache as IFxRateCache
    participant Provider as ITreasuryFxRateProvider
    participant Treasury as Treasury API
    participant DB as SQLite

    Client->>+API: GET /purchases/{id}?currencyKey=Austria-Euro
    API->>+UC: ExecuteAsync(...)
    
    Note over UC: (Purchase retrieval omitted for brevity)
    
    UC->>+FxResolver: ResolveRateAsync(currencyKey, anchorDate)
    
    FxResolver->>+Cache: GetLatestRateAsync(...)
    Cache->>DB: SELECT ...
    DB-->>Cache: No rows
    Cache-->>-FxResolver: null (cache miss)
    
    FxResolver->>+Provider: GetLatestRateAsync(currencyKey, anchorDate, 6)
    Provider->>+Treasury: GET /rates_of_exchange?<br/>filter=currency_key:eq:Austria-Euro,record_date:lte:2024-12-20,...<br/>sort=-record_date&page[size]=1
    Treasury-->>-Provider: JSON response with rate
    Provider-->>-FxResolver: FxRate(0.95, 2024-12-18)
    
    Note over FxResolver: Cache the rate
    
    FxResolver->>+Cache: UpsertAsync(rate)
    Cache->>+DB: INSERT OR REPLACE INTO fx_rate_cache
    DB-->>-Cache: ✓
    Cache-->>-FxResolver: ✓
    
    FxResolver-->>-UC: FxRate(0.95, 2024-12-18)
    
    UC-->>-API: ConvertedPurchaseResponse
    API-->>-Client: 200 OK
```

## Error Path — No Rate in Window (422)

```mermaid
sequenceDiagram
    actor Client
    participant API as PurchaseEndpoints
    participant MW as ExceptionHandlingMiddleware
    participant UC as GetPurchaseConvertedUseCase
    participant FxResolver as FxRateResolver
    participant Cache as IFxRateCache
    participant Provider as ITreasuryFxRateProvider

    Client->>+API: GET /purchases/{id}?currencyKey=Test-Currency
    API->>+MW: Next()
    MW->>+UC: ExecuteAsync(...)
    
    Note over UC: (Purchase retrieval omitted)
    
    UC->>+FxResolver: ResolveRateAsync(currencyKey, anchorDate)
    
    FxResolver->>+Cache: GetLatestRateAsync(...)
    Cache-->>-FxResolver: null
    
    FxResolver->>+Provider: GetLatestRateAsync(...)
    Provider-->>-FxResolver: null (no rate found)
    
    Note over FxResolver: No rate in 6-month window
    
    FxResolver-->>UC: throw FxConversionUnavailableException<br/>"No exchange rate available for Test-Currency within 6 months"
    
    UC-->>MW: (exception propagates)
    
    Note over MW: Map to ProblemDetails
    
    MW-->>-API: ProblemDetails response
    API-->>-Client: 422 Unprocessable Entity<br/>code: FX-4220<br/>detail: "No exchange rate available..."
```

## Error Path — Upstream Failure with Cache Fallback (200)

```mermaid
sequenceDiagram
    actor Client
    participant API as PurchaseEndpoints
    participant UC as GetPurchaseConvertedUseCase
    participant FxResolver as FxRateResolver
    participant Cache as IFxRateCache
    participant Provider as ITreasuryFxRateProvider

    Client->>+API: GET /purchases/{id}?currencyKey=Australia-Dollar
    API->>+UC: ExecuteAsync(...)
    
    UC->>+FxResolver: ResolveRateAsync(...)
    
    FxResolver->>+Cache: GetLatestRateAsync(...)
    Cache-->>-FxResolver: FxRate(1.612, 2024-12-10)<br/>(stale but within window)
    
    Note over FxResolver: Cache hit!<br/>Return immediately<br/>(no upstream call)
    
    FxResolver-->>-UC: FxRate(1.612, 2024-12-10)
    
    UC-->>-API: ConvertedPurchaseResponse
    API-->>-Client: 200 OK<br/>(uses cached rate)
```

## Error Path — Upstream Failure, No Cache (503)

```mermaid
sequenceDiagram
    actor Client
    participant API as PurchaseEndpoints
    participant MW as ExceptionHandlingMiddleware
    participant UC as GetPurchaseConvertedUseCase
    participant FxResolver as FxRateResolver
    participant Cache as IFxRateCache
    participant Provider as ITreasuryFxRateProvider

    Client->>+API: GET /purchases/{id}?currencyKey=Australia-Dollar
    API->>+MW: Next()
    MW->>+UC: ExecuteAsync(...)
    
    UC->>+FxResolver: ResolveRateAsync(...)
    
    FxResolver->>+Cache: GetLatestRateAsync(...)
    Cache-->>-FxResolver: null (no cached rate)
    
    FxResolver->>+Provider: GetLatestRateAsync(...)
    Provider-->>-FxResolver: throw HttpRequestException<br/>"Timeout"
    
    Note over FxResolver: Upstream failed<br/>No cached fallback<br/>available
    
    FxResolver-->>UC: throw FxUpstreamUnavailableException<br/>"Foreign exchange upstream service is unavailable..."
    
    UC-->>MW: (exception propagates)
    
    Note over MW: Map to ProblemDetails
    
    MW-->>-API: ProblemDetails response
    API-->>-Client: 503 Service Unavailable<br/>code: FX-5030<br/>detail: "Foreign exchange upstream service is unavailable..."
```

## Key Steps

1. **Purchase Retrieval**: Query DB for purchase by composite key (cardId, purchaseId)
2. **FX Rate Resolution**: Cache-first strategy (see [10-activity-fx-resolution.md](./10-activity-fx-resolution.md))
3. **Conversion**: `convertedAmount = Round(amountUsd * exchangeRate, 2)`
4. **Response**: Original USD amount + rate + converted amount + rate date

## Error Codes

- **RES-4040** (404): Purchase not found or doesn't belong to specified card
- **FX-4220** (422): No exchange rate available in 6-month window
- **FX-5030** (503): Upstream unavailable and no cached fallback
