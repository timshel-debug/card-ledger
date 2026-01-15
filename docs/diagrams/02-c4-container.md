# C4 Container Diagram — CardService Containers

## Purpose
Shows the logical containers (deployable units) within CardService and their relationships.

## Assumptions
- Clean Architecture with strict layer dependencies: Domain ← Application ← Infrastructure ← API
- All containers run in a single process (ASP.NET Core)
- EF Core mediates all database access

## Diagram

```mermaid
C4Container
    title Container Diagram for CardService

    Person(client, "Client", "Consumer of CardService API")

    System_Boundary(cardServiceBoundary, "CardService") {
        Container(api, "API Layer", ".NET 10, ASP.NET Core Minimal API", "HTTP endpoints, middleware, request/response mapping")
        Container(application, "Application Layer", ".NET 10 Class Library", "Use cases, DTOs, port interfaces, FxRateResolver")
        Container(infrastructure, "Infrastructure Layer", ".NET 10 Class Library", "EF Core, repositories, Treasury API client, caching, Polly resilience")
        Container(domain, "Domain Layer", ".NET 10 Class Library", "Entities, value objects, domain invariants")
    }

    ContainerDb(db, "SQLite Database", "SQLite", "cards, purchases, fx_rate_cache tables")
    System_Ext(treasury, "Treasury FX API", "U.S. Treasury Fiscal Data Service")

    Rel(client, api, "HTTPS/JSON", "REST API")
    Rel(api, application, "Invokes use cases", "In-process")
    Rel(application, infrastructure, "Calls via ports", "In-process")
    Rel(application, domain, "Uses entities/VOs", "In-process")
    Rel(infrastructure, domain, "Implements aggregates", "In-process")
    Rel(infrastructure, db, "EF Core", "SQL")
    Rel(infrastructure, treasury, "HTTP/JSON", "Exchange rate queries")
```

## Layer Responsibilities

- **API**: Endpoint routing, request validation, error middleware, OpenAPI documentation
- **Application**: Orchestration (use cases), business workflows, DTO mapping, port definitions
- **Infrastructure**: Persistence (EF Core), external integrations (Treasury API), caching, resilience
- **Domain**: Business entities (`Card`, `PurchaseTransaction`), value objects (`Money`, `CardNumber`, `FxRate`), invariants

## Dependency Rules
High-level policies (use cases) depend only on abstractions. Infrastructure implements ports defined in Application.
