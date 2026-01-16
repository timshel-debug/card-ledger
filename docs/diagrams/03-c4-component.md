# C4 Component Diagram — CardService Components

## Purpose
Detailed component view within CardService showing key classes and their interactions.

## Assumptions
- Components are C# types (classes, interfaces)
- Use case classes implement application logic
- Repositories implement port interfaces for persistence
- FxRateResolver orchestrates FX rate retrieval with caching

## Diagram

```mermaid
flowchart TB
    subgraph API["API Layer"]
        cardEndpoints["CardEndpoints<br/>(Static class)<br/>POST /cards, GET /balance"]
        purchaseEndpoints["PurchaseEndpoints<br/>(Static class)<br/>POST /purchases, GET /purchase"]
        exceptionMiddleware["ExceptionHandlingMiddleware<br/>(Middleware)<br/>Maps exceptions to ProblemDetails"]
    end

    subgraph Application["Application Layer"]
        createCardUseCase["CreateCardUseCase<br/>Validates, hashes card number, persists"]
        createPurchaseUseCase["CreatePurchaseUseCase<br/>Validates, ensures card exists, persists"]
        getPurchaseConvertedUseCase["GetPurchaseConvertedUseCase<br/>Retrieves purchase, converts to currency"]
        getBalanceUseCase["GetAvailableBalanceUseCase<br/>Computes balance, optionally converts"]
        fxResolverInterface["IFxRateResolver<br/>(Port interface)<br/>Abstraction for FX resolution"]
        fxResolver["FxRateResolver<br/>(Service)<br/>Resolves FX rate with cache/fallback"]
        cardRepo["ICardRepository<br/>(Port interface)"]
        purchaseRepo["IPurchaseRepository<br/>(Port interface)"]
        fxCache["IFxRateCache<br/>(Port interface)"]
        fxProvider["ITreasuryFxRateProvider<br/>(Port interface)"]
    end

    subgraph Infrastructure["Infrastructure Layer"]
        cardRepoImpl["CardRepository<br/>(EF Core)"]
        purchaseRepoImpl["PurchaseRepository<br/>(EF Core)"]
        fxCacheImpl["FxRateCacheRepository<br/>(EF Core)"]
        treasuryClient["TreasuryFxRateProvider<br/>(HTTP client with Polly)<br/>Retry, Timeout, Circuit Breaker"]
        dbContext["AppDbContext<br/>(EF Core DbContext)"]
    end

    db[("SQLite Database<br/>cards, purchases, fx_rate_cache")]

    cardEndpoints --> createCardUseCase
    cardEndpoints --> getBalanceUseCase
    purchaseEndpoints --> createPurchaseUseCase
    purchaseEndpoints --> getPurchaseConvertedUseCase
    
    createCardUseCase --> cardRepo
    createPurchaseUseCase --> cardRepo
    createPurchaseUseCase --> purchaseRepo
    getPurchaseConvertedUseCase --> purchaseRepo
    getPurchaseConvertedUseCase --> fxResolverInterface
    getBalanceUseCase --> cardRepo
    getBalanceUseCase --> purchaseRepo
    getBalanceUseCase --> fxResolverInterface
    
    fxResolverInterface -.->|implements| fxResolver
    fxResolver --> fxCache
    fxResolver --> fxProvider
    
    cardRepo -.->|implements| cardRepoImpl
    purchaseRepo -.->|implements| purchaseRepoImpl
    fxCache -.->|implements| fxCacheImpl
    fxProvider -.->|implements| treasuryClient
    
    cardRepoImpl --> dbContext
    purchaseRepoImpl --> dbContext
    fxCacheImpl --> dbContext
    treasuryClient -->|HTTP| Treasury[Treasury API]
    dbContext --> db
    
    style API fill:#E1F5FF
    style Application fill:#FFF9E1
    style Infrastructure fill:#FFE1E1
```

## Key Component Responsibilities

### API Components
- **CardEndpoints**: Route mapping for card operations
- **PurchaseEndpoints**: Route mapping for purchase operations
- **ExceptionHandlingMiddleware**: Global exception handler with structured error codes

### Application Components
- **Use Cases**: Single-responsibility orchestrators (one per API operation)
- **FxRateResolver**: Encapsulates FX rate resolution logic with cache-first strategy
- **Port Interfaces**: Abstract persistence and external services

### Infrastructure Components
- **Repositories**: Concrete EF Core implementations of ports
- **TreasuryFxRateProvider**: HTTP client with Polly retry/timeout/circuit breaker
- **AppDbContext**: EF Core database session
