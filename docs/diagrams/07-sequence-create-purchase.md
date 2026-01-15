# Sequence Diagram — Create Purchase

## Purpose
Shows the flow for `POST /cards/{cardId}/purchases` including card existence check and validation.

## API Endpoint
```
POST /cards/3fa85f64-5717-4562-b3fc-2c963f66afa6/purchases
Content-Type: application/json

{
  "description": "Coffee at Starbucks",
  "transactionDate": "2024-12-20",
  "amountUsd": 4.50
}
```

## Happy Path

```mermaid
sequenceDiagram
    actor Client
    participant API as PurchaseEndpoints
    participant MW as ExceptionHandlingMiddleware
    participant UC as CreatePurchaseUseCase
    participant CardRepo as ICardRepository
    participant PurchaseRepo as IPurchaseRepository
    participant DB as SQLite

    Client->>+API: POST /cards/{cardId}/purchases<br/>{description, date, amount}
    API->>+MW: Next()
    MW->>+UC: ExecuteAsync(cardId, request)
    
    Note over UC: Validate description ≤ 50 chars
    Note over UC: Validate amount > 0
    Note over UC: Validate transaction date
    
    UC->>+CardRepo: ExistsAsync(cardId)
    CardRepo->>+DB: SELECT COUNT(*) FROM cards WHERE id = ?
    DB-->>-CardRepo: 1
    CardRepo-->>-UC: true
    
    Note over UC: Create PurchaseTransaction entity<br/>Generate GUID<br/>Convert amount to cents
    
    UC->>+PurchaseRepo: AddAsync(purchase)
    PurchaseRepo->>+DB: INSERT INTO purchases
    DB-->>-PurchaseRepo: ✓
    PurchaseRepo-->>-UC: ✓
    
    UC-->>-MW: CreatePurchaseResponse{purchaseId}
    MW-->>-API: CreatePurchaseResponse
    API-->>-Client: 201 Created<br/>Location: /cards/{cardId}/purchases/{purchaseId}<br/>{purchaseId}
```

## Error Path — Card Not Found

```mermaid
sequenceDiagram
    actor Client
    participant API as PurchaseEndpoints
    participant MW as ExceptionHandlingMiddleware
    participant UC as CreatePurchaseUseCase
    participant CardRepo as ICardRepository
    participant DB as SQLite

    Client->>+API: POST /cards/{invalidCardId}/purchases<br/>{...}
    API->>+MW: Next()
    MW->>+UC: ExecuteAsync(cardId, request)
    
    Note over UC: Validation passes
    
    UC->>+CardRepo: ExistsAsync(cardId)
    CardRepo->>+DB: SELECT COUNT(*) FROM cards WHERE id = ?
    DB-->>-CardRepo: 0
    CardRepo-->>-UC: false
    
    UC-->>MW: throw ResourceNotFoundException<br/>"Card with ID {cardId} not found"
    
    Note over MW: Map to ProblemDetails
    
    MW-->>-API: ProblemDetails response
    API-->>-Client: 404 Not Found<br/>code: RES-4040<br/>detail: "Card with ID ... not found"
```

## Error Path — Validation Failure

```mermaid
sequenceDiagram
    actor Client
    participant API as PurchaseEndpoints
    participant MW as ExceptionHandlingMiddleware
    participant UC as CreatePurchaseUseCase

    Client->>+API: POST /cards/{cardId}/purchases<br/>{description: "51 chars..."}
    API->>+MW: Next()
    MW->>+UC: ExecuteAsync(cardId, request)
    
    Note over UC: Validate description<br/>❌ > 50 characters
    
    UC-->>MW: throw ValidationException<br/>"Description cannot exceed 50 characters"
    
    Note over MW: Map to ProblemDetails
    
    MW-->>-API: ProblemDetails response
    API-->>-Client: 400 Bad Request<br/>code: VAL-0001<br/>detail: "Description cannot exceed 50 characters"
```

## Key Steps

1. **Validation**: Description length (≤50), amount (positive), date format
2. **Card Existence Check**: Query DB to ensure card exists before creating purchase
3. **Entity Creation**: Domain `PurchaseTransaction` with GUID, cents conversion
4. **Persistence**: EF Core `INSERT` with FK constraint to cards
5. **Response**: 201 Created with `Location` header and `purchaseId`

## Validation Rules

- **Description**: Non-empty, max 50 characters
- **TransactionDate**: Valid date format (ISO 8601 date-only)
- **AmountUsd**: Positive decimal, stored as integer cents
- **CardId**: Must reference existing card

## Error Codes

- **VAL-0001** (400): Validation error (description too long, negative amount, invalid date)
- **RES-4040** (404): Card not found
