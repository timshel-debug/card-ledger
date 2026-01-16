# C4 Context Diagram — CardService System

## Purpose
High-level view showing CardService in its environment with external actors and systems.

## Assumptions
- Client applications (mobile/web) interact via HTTPS/REST
- Treasury Reporting Rates of Exchange API is the authoritative FX data source
- SQLite is used for persistence (file-based, no external DB server)

## Diagram

```mermaid
C4Context
    title System Context Diagram for CardService

    Person(client, "Client Application", "Mobile or web app that manages cards and purchases")
    
    System(cardService, "CardService API", "Manages credit cards, purchase transactions, and FX conversions using Treasury data")
    
    System_Ext(treasury, "U.S. Treasury FX API", "Treasury Reporting Rates of Exchange - provides official exchange rates")
    
    SystemDb(sqlite, "SQLite Database", "Persistent storage for cards, purchases, and cached FX rates")

    Rel(client, cardService, "Creates cards, records purchases, requests conversions", "HTTPS/JSON")
    Rel(cardService, treasury, "Fetches exchange rates", "HTTPS/JSON")
    Rel(cardService, sqlite, "Reads/writes", "EF Core")
    
    UpdateLayoutConfig($c4ShapeInRow="1", $c4BoundaryInRow="1")
```

## Key Interactions

1. **Client → CardService**: RESTful API calls for card/purchase management and currency conversion
2. **CardService → Treasury API**: On-demand FX rate retrieval (with caching and resilience policies)
3. **CardService → SQLite**: Persistence of cards, purchases, and cached FX rates

## Security Boundary
Card numbers are hashed before storage; only last 4 digits retained in plaintext.
