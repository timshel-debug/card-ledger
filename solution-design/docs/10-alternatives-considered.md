# Alternatives Considered

## A1. In-memory persistence + JSON file snapshot
- Pros: simplest, no EF Core
- Cons: harder to enforce constraints; concurrency and durability concerns
- Rejected: less “production-ready” and weaker integrity

## A2. LiteDB (embedded document DB)
- Pros: embedded, no external dependency, simple
- Cons: less standard in .NET enterprise environments
- Viable alternative if EF Core deemed too heavy

## A3. Postgres/MySQL
- Pros: production-grade scalability
- Cons: violates “no separate DB install” requirement for this exercise
- Deferred for future scaling

## A4. Accept ISO 4217 currency codes
- Pros: more familiar for clients
- Cons: Treasury dataset does not clearly provide ISO codes; mapping introduces external dependencies and ambiguity
- Rejected for determinism; adopt later with explicit mapping table if desired
