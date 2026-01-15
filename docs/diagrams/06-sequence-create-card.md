# Sequence Diagram — Create Card

## Purpose
Shows the flow for `POST /cards` including validation, hashing, persistence, and error handling.

## API Endpoint
```
POST /cards
Content-Type: application/json

{
  "cardNumber": "4111111111111111",
  "creditLimitUsd": 1000.00
}
```

## Happy Path

```mermaid
sequenceDiagram
    actor Client
    participant API as CardEndpoints
    participant MW as ExceptionHandlingMiddleware
    participant UC as CreateCardUseCase
    participant Repo as ICardRepository
    participant DB as SQLite

    Client->>+API: POST /cards<br/>{cardNumber, creditLimitUsd}
    API->>+MW: Next()
    MW->>+UC: ExecuteAsync(request)
    
    Note over UC: Validate card number<br/>(16 digits, numeric)
    Note over UC: Validate credit limit > 0
    Note over UC: Hash card number<br/>SHA-256(cardNumber)
    
    UC->>+Repo: ExistsAsync(cardNumberHash)
    Repo->>+DB: SELECT EXISTS WHERE card_number_hash = ?
    DB-->>-Repo: false
    Repo-->>-UC: false
    
    Note over UC: Create Card aggregate<br/>Generate GUID
    
    UC->>+Repo: AddAsync(card)
    Repo->>+DB: INSERT INTO cards
    DB-->>-Repo: ✓
    Repo-->>-UC: ✓
    
    UC-->>-MW: CreateCardResponse{cardId}
    MW-->>-API: CreateCardResponse
    API-->>-Client: 201 Created<br/>Location: /cards/{cardId}<br/>{cardId}
```

## Error Path — Validation Failure

```mermaid
sequenceDiagram
    actor Client
    participant API as CardEndpoints
    participant MW as ExceptionHandlingMiddleware
    participant UC as CreateCardUseCase

    Client->>+API: POST /cards<br/>{cardNumber: "123", ...}
    API->>+MW: Next()
    MW->>+UC: ExecuteAsync(request)
    
    Note over UC: Validate card number<br/>❌ Not 16 digits
    
    UC-->>MW: throw ValidationException<br/>"Card number must be exactly 16 digits"
    
    Note over MW: Catch exception<br/>Map to ProblemDetails
    
    MW-->>-API: ProblemDetails response
    API-->>-Client: 400 Bad Request<br/>code: VAL-0001<br/>detail: "Card number must be exactly 16 digits"
```

## Error Path — Duplicate Card Number

```mermaid
sequenceDiagram
    actor Client
    participant API as CardEndpoints
    participant MW as ExceptionHandlingMiddleware
    participant UC as CreateCardUseCase
    participant Repo as ICardRepository
    participant DB as SQLite

    Client->>+API: POST /cards<br/>{cardNumber: "4111111111111111", ...}
    API->>+MW: Next()
    MW->>+UC: ExecuteAsync(request)
    
    Note over UC: Hash card number
    
    UC->>+Repo: ExistsAsync(cardNumberHash)
    Repo->>+DB: SELECT EXISTS WHERE card_number_hash = ?
    DB-->>-Repo: true
    Repo-->>-UC: true
    
    UC-->>MW: throw DuplicateResourceException<br/>"A card with this number already exists"
    
    Note over MW: Map to ProblemDetails
    
    MW-->>-API: ProblemDetails response
    API-->>-Client: 409 Conflict<br/>code: DB-4090<br/>detail: "A card with this number already exists"
```

## Key Steps

1. **Validation**: Card number (16 digits, numeric), credit limit (positive)
2. **Hashing**: SHA-256(cardNumber) with salt for uniqueness check
3. **Uniqueness Check**: Query DB for existing `card_number_hash`
4. **Aggregate Creation**: Domain `Card` entity with GUID and hashed number
5. **Persistence**: EF Core `INSERT` with unique constraint enforcement
6. **Response**: 201 Created with `Location` header and `cardId`

## Error Codes

- **VAL-0001** (400): Validation error (invalid card number, negative limit)
- **DB-4090** (409): Duplicate card number (hash collision detected)
