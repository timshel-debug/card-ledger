# Architecture Diagrams (Mermaid)

## C4-ish Context Diagram

```mermaid
flowchart LR
  Client[Client / Consumer] --> API[Card Service API]
  API --> DB[(SQLite DB)]
  API --> Treasury[U.S. Treasury Fiscal Data API<br/>rates_of_exchange]
```

## Container / Component Diagram

```mermaid
flowchart TB
  subgraph API_Layer[API Layer]
    Endpoints[Minimal API Endpoints]
    Middleware[Error Mapping + Validation]
  end

  subgraph App_Layer[Application Layer]
    UC1[CreateCard Use Case]
    UC2[CreatePurchase Use Case]
    UC3[GetPurchaseConverted Use Case]
    UC4[GetAvailableBalance Use Case]
  end

  subgraph Domain_Layer[Domain Layer]
    Card[Card Aggregate]
    Purchase[PurchaseTransaction]
    Money[Money VO]
    FxRate[FxRate VO]
  end

  subgraph Infra_Layer[Infrastructure Layer]
    Repos[EF Repositories]
    Db[EF Core DbContext]
    FxResolver[FxRateResolver]
    FxCache[Fx Rate Cache Store]
    TreasuryClient[Treasury API Client]
    Policies[Resilience Policies]
  end

  Endpoints --> Middleware --> UC1
  Middleware --> UC2
  Middleware --> UC3
  Middleware --> UC4

  UC1 --> Card
  UC2 --> Purchase
  UC3 --> FxResolver
  UC4 --> FxResolver

  UC1 --> Repos
  UC2 --> Repos
  UC3 --> Repos
  UC4 --> Repos

  Repos --> Db --> DB[(SQLite)]

  FxResolver --> FxCache
  FxResolver --> TreasuryClient --> Treasury[U.S. Treasury API]
  TreasuryClient --> Policies
  FxCache --> DB
```

## Sequence — Get Purchase Converted

```mermaid
sequenceDiagram
  autonumber
  participant C as Client
  participant API as API
  participant P as PurchaseRepo
  participant FX as FxRateResolver
  participant Cache as FxCache
  participant T as Treasury API

  C->>API: GET /cards/{cardId}/purchases/{purchaseId}?currencyKey=...
  API->>P: Load purchase (cardId,purchaseId)
  P-->>API: Purchase
  API->>FX: ResolveRate(currencyKey, purchaseDate)
  FX->>Cache: Query cached rate in 6mo window
  alt cache hit
    Cache-->>FX: rate
  else cache miss
    FX->>T: GET rates_of_exchange filter/sort/limit=1
    T-->>FX: rate
    FX->>Cache: Upsert rate
  end
  FX-->>API: rate
  API-->>C: Purchase + exchange rate + converted amount
```

## Data Model ERD (logical)

```mermaid
erDiagram
  CARD ||--o{ PURCHASE : has
  CARD {
    string Id
    string CardNumberHash
    string Last4
    long CreditLimitCents
    datetime CreatedUtc
  }
  PURCHASE {
    string Id
    string CardId
    string Description
    date TransactionDate
    long AmountCents
    datetime CreatedUtc
  }
  FX_RATE_CACHE {
    string CurrencyKey
    date RecordDate
    decimal ExchangeRate
    datetime CachedUtc
  }
```

---

## Comprehensive Diagram Suite

For detailed architectural documentation including C4 models, sequence diagrams, deployment views, security boundaries, and observability instrumentation, see the **[Diagram Suite Index](../../docs/diagrams/00-index.md)**.

The diagram suite includes:
- **Structural Views**: C4 Context, Container, Component models; Deployment topologies
- **Behavioral Views**: Sequence diagrams for all 4 API endpoints; FX resolution activity flow; Error mapping flowchart
- **Data Views**: ERD with FK relationships; Data Flow Diagrams (layer-to-layer)
- **Cross-Cutting Views**: Security boundaries (card number hashing, TLS, logging redaction); Observability (logging, health checks, metrics, tracing)
