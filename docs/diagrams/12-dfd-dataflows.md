# Data Flow Diagram — Layer-to-Layer Data Movement

## Purpose
Shows how data flows through the Clean Architecture layers for typical operations.

## Level 0 — System Context Data Flow

```mermaid
flowchart LR
    Client[Client Application] -->|HTTP Requests<br/>JSON payloads| CardService[CardService API]
    CardService -->|HTTP Requests<br/>Query params| Treasury[Treasury FX API]
    Treasury -->|JSON Responses<br/>Exchange rates| CardService
    CardService -->|SQL Commands<br/>Entity data| SQLite[(SQLite Database)]
    SQLite -->|Query Results<br/>Rows| CardService
    CardService -->|HTTP Responses<br/>JSON payloads| Client
    
    style Client fill:#87CEEB
    style CardService fill:#90EE90
    style Treasury fill:#FFD700
    style SQLite fill:#DDA0DD
```

## Level 1 — Internal Layer Data Flow

### Create Card Flow

```mermaid
flowchart TD
    Client[Client] -->|POST /cards<br/>CreateCardRequest| API[API Layer<br/>CardEndpoints]
    API -->|CreateCardRequest DTO| AppLayer[Application Layer<br/>CreateCardUseCase]
    
    AppLayer -->|Validate & hash| DomainLogic[Domain Logic]
    DomainLogic -->|Card aggregate| AppLayer
    
    AppLayer -->|Card entity| Infrastructure[Infrastructure Layer<br/>CardRepository]
    Infrastructure -->|INSERT SQL<br/>EF Core mapping| DB[(SQLite)]
    
    DB -->|Row inserted| Infrastructure
    Infrastructure -->|Persisted Card| AppLayer
    AppLayer -->|CreateCardResponse DTO| API
    API -->|201 Created<br/>JSON response| Client
    
    style Client fill:#87CEEB
    style API fill:#FFB6C1
    style AppLayer fill:#98FB98
    style DomainLogic fill:#FFD700
    style Infrastructure fill:#DDA0DD
    style DB fill:#696969,color:#FFF
```

### Get Purchase Converted Flow

```mermaid
flowchart TD
    Client[Client] -->|GET /purchases/ID?currencyKey=X<br/>Query params| API[API Layer<br/>PurchaseEndpoints]
    API -->|cardId, purchaseId, currencyKey| AppLayer[Application Layer<br/>GetPurchaseConvertedUseCase]
    
    AppLayer -->|Query by ID| Infrastructure1[Infrastructure<br/>PurchaseRepository]
    Infrastructure1 -->|SELECT SQL| DB[(SQLite)]
    DB -->|Purchase row| Infrastructure1
    Infrastructure1 -->|PurchaseTransaction entity| AppLayer
    
    AppLayer -->|currencyKey, anchorDate| FxResolver[FxRateResolver]
    
    FxResolver -->|Query cache| Infrastructure2[Infrastructure<br/>FxRateCacheRepository]
    Infrastructure2 -->|SELECT SQL| DB
    DB -->|Cached rate row| Infrastructure2
    Infrastructure2 -->|FxRate or null| FxResolver
    
    FxResolver -->|HTTP GET<br/>if cache miss| TreasuryClient[TreasuryFxRateProvider]
    TreasuryClient -->|HTTPS Request| Treasury[Treasury API]
    Treasury -->|JSON Response<br/>Exchange rate data| TreasuryClient
    TreasuryClient -->|Parsed FxRate| FxResolver
    
    FxResolver -->|Upsert rate| Infrastructure2
    Infrastructure2 -->|INSERT OR REPLACE| DB
    
    FxResolver -->|Resolved FxRate| AppLayer
    
    AppLayer -->|Convert & map| DomainLogic[Domain<br/>Money conversion]
    DomainLogic -->|Converted amounts| AppLayer
    
    AppLayer -->|ConvertedPurchaseResponse DTO| API
    API -->|200 OK<br/>JSON response| Client
    
    style Client fill:#87CEEB
    style API fill:#FFB6C1
    style AppLayer fill:#98FB98
    style DomainLogic fill:#FFD700
    style FxResolver fill:#98FB98
    style Infrastructure1 fill:#DDA0DD
    style Infrastructure2 fill:#DDA0DD
    style TreasuryClient fill:#DDA0DD
    style Treasury fill:#FF6347
    style DB fill:#696969,color:#FFF
```

## Data Transformation Pipeline

### Request → Response Flow

```mermaid
flowchart LR
    subgraph "Client Side"
        ClientReq[HTTP Request<br/>JSON Body]
    end
    
    subgraph "API Layer"
        Deserialize[Deserialize<br/>to Request DTO]
        Validate[Validate<br/>Request DTO]
    end
    
    subgraph "Application Layer"
        UseCaseOrch[Use Case<br/>Orchestration]
        DTOMap1[Map DTO<br/>to Domain]
    end
    
    subgraph "Domain Layer"
        DomainEntities[Domain Entities<br/>Value Objects]
        BusinessRules[Apply<br/>Business Rules]
    end
    
    subgraph "Infrastructure Layer"
        EFMapping[EF Core<br/>Entity Mapping]
        SQLExec[Execute<br/>SQL Commands]
    end
    
    subgraph "Database"
        SQLiteStore[(SQLite<br/>Persisted Data)]
    end
    
    subgraph "Application Layer (Response)"
        DTOMap2[Map Domain<br/>to Response DTO]
    end
    
    subgraph "API Layer (Response)"
        Serialize[Serialize<br/>Response DTO]
    end
    
    subgraph "Client Side (Response)"
        ClientResp[HTTP Response<br/>JSON Body]
    end
    
    ClientReq --> Deserialize
    Deserialize --> Validate
    Validate --> UseCaseOrch
    UseCaseOrch --> DTOMap1
    DTOMap1 --> DomainEntities
    DomainEntities --> BusinessRules
    BusinessRules --> EFMapping
    EFMapping --> SQLExec
    SQLExec --> SQLiteStore
    SQLiteStore --> SQLExec
    SQLExec --> EFMapping
    EFMapping --> DomainEntities
    DomainEntities --> DTOMap2
    DTOMap2 --> UseCaseOrch
    UseCaseOrch --> Serialize
    Serialize --> ClientResp
    
    style ClientReq fill:#87CEEB
    style ClientResp fill:#87CEEB
    style DomainEntities fill:#FFD700
    style BusinessRules fill:#FFD700
    style SQLiteStore fill:#696969,color:#FFF
```

## Data Types by Layer

### API Layer (DTOs)
```csharp
// Request DTOs
CreateCardRequest { string CardNumber; decimal CreditLimitUsd; }
CreatePurchaseRequest { string Description; DateOnly TransactionDate; decimal AmountUsd; }

// Response DTOs
CreateCardResponse { Guid CardId; }
ConvertedPurchaseResponse { Guid PurchaseId; string Description; decimal AmountUsd; string CurrencyKey; decimal ExchangeRate; decimal ConvertedAmount; }
```

### Application Layer (DTOs + Domain References)
```csharp
// Use cases work with DTOs + Domain entities
UseCase.ExecuteAsync(RequestDTO dto) → ResponseDTO
// Internally: Domain entities, value objects, port calls
```

### Domain Layer (Pure Business Logic)
```csharp
// Entities
Card { CardId, CardNumber, CreditLimit, Purchases }
PurchaseTransaction { PurchaseId, CardId, Description, TransactionDate, Amount }

// Value Objects
Money { Amount, Currency }
CardNumber { Value (16 digits) }
FxRate { CurrencyKey, RecordDate, ExchangeRate }
```

### Infrastructure Layer (EF Core Entities)
```csharp
// EF Core entity configurations
CardConfiguration : IEntityTypeConfiguration<Card>
// Maps Domain.Card → Database.cards table
// Handles card_number_hash storage, cents conversion
```

### Database Layer (SQLite Schema)
```sql
-- Persisted as rows
cards: id, card_number_hash, last4, credit_limit_cents, created_utc
purchases: id, card_id, description, transaction_date, amount_cents, created_utc
fx_rate_cache: currency_key, record_date, exchange_rate, cached_utc
```

## External Data Flow

### Treasury API Integration

```mermaid
sequenceDiagram
    participant App as CardService<br/>Application Layer
    participant Infra as TreasuryFxRateProvider<br/>(Infrastructure)
    participant Polly as Polly Resilience<br/>(Retry/Timeout/CB)
    participant HTTP as HttpClient
    participant Treasury as Treasury API

    App->>Infra: GetLatestRateAsync(currencyKey, anchorDate, monthsBack)
    Infra->>Infra: Build query URL with filters
    Note over Infra: filter=currency_key:eq:X,<br/>record_date:lte:Y,record_date:gte:Z<br/>sort=-record_date&page[size]=1
    
    Infra->>Polly: Execute with policies
    Polly->>HTTP: GET {url}
    HTTP->>Treasury: HTTPS Request
    Treasury-->>HTTP: JSON Response
    HTTP-->>Polly: HttpResponseMessage
    Polly-->>Infra: Response (with retries if needed)
    
    Infra->>Infra: Parse JSON → FxRate
    Infra-->>App: FxRate or null
```

**Request Example:**
```
GET https://api.fiscaldata.treasury.gov/services/api/fiscal_service/v1/accounting/od/rates_of_exchange
?filter=country_currency_desc:eq:Australia-Dollar,record_date:lte:2024-12-20,record_date:gte:2024-06-20
&sort=-record_date
&page[size]=1
```

**Response Example:**
```json
{
  "data": [
    {
      "country_currency_desc": "Australia-Dollar",
      "exchange_rate": "1.612",
      "record_date": "2024-12-15"
    }
  ]
}
```

## Data Volume Estimates

### Typical Operation Data Sizes

| Operation | Request Size | Response Size | DB Reads | DB Writes |
|---|---|---|---|---|
| Create Card | 50 bytes | 50 bytes | 1 SELECT | 1 INSERT |
| Create Purchase | 100 bytes | 50 bytes | 2 SELECTs | 1 INSERT |
| Get Purchase Converted | 20 bytes | 200 bytes | 3 SELECTs | 0-1 INSERT |
| Get Balance | 20 bytes | 150 bytes | 2 SELECTs + 1 aggregate | 0 |

### Database Growth
- **Cards**: ~150 bytes/row (GUID + hash + metadata)
- **Purchases**: ~200 bytes/row
- **FX Cache**: ~100 bytes/row
- **Indexes**: 30-50% overhead

Example: 10,000 cards × 100 purchases each = 1M purchases ≈ 250 MB database file
