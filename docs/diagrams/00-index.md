# Diagram Suite Index

This directory contains comprehensive architectural and design diagrams for the CardService system. All diagrams are text-based (Mermaid in Markdown) for version control and GitHub rendering.

## Structural Diagrams (C4 Model)

- [01-c4-context.md](./01-c4-context.md) — System Context: CardService in its environment
- [02-c4-container.md](./02-c4-container.md) — Container View: API, Application, Infrastructure, Database, External APIs
- [03-c4-component.md](./03-c4-component.md) — Component View: Endpoints, Middleware, Use Cases, Services, Repositories

## Deployment & Infrastructure

- [04-deployment.md](./04-deployment.md) — Deployment topologies (local dev, containerized)

## Data Architecture

- [05-data-model-erd.md](./05-data-model-erd.md) — Entity-Relationship Diagram for cards, purchases, fx_rate_cache

## Behavioral Diagrams (Sequences)

- [06-sequence-create-card.md](./06-sequence-create-card.md) — POST /cards flow with validation and duplicate detection
- [07-sequence-create-purchase.md](./07-sequence-create-purchase.md) — POST /cards/{cardId}/purchases with card existence check
- [08-sequence-get-purchase-converted.md](./08-sequence-get-purchase-converted.md) — GET purchase with FX conversion (cache, upstream, fallback, errors)
- [09-sequence-get-balance.md](./09-sequence-get-balance.md) — GET balance with optional currency conversion

## Process & Activity Diagrams

- [10-activity-fx-resolution.md](./10-activity-fx-resolution.md) — FxRateResolver algorithm activity flow
- [11-flow-error-mapping.md](./11-flow-error-mapping.md) — Exception-to-ProblemDetails mapping flowchart

## Data Flow

- [12-dfd-dataflows.md](./12-dfd-dataflows.md) — Data Flow Diagram showing layer-to-layer data movement

## Cross-Cutting Concerns

- [13-security-boundaries.md](./13-security-boundaries.md) — Security controls: card number hashing, sensitive data handling
- [14-observability.md](./14-observability.md) — Logging, health checks, and monitoring touchpoints

---

## Diagram Conventions

- **Mermaid syntax** is used throughout for GitHub compatibility
- Component names match actual C# type names where applicable
- Error paths are explicitly diagrammed
- All flows reflect the implemented system (no hypothetical components)

## Related Documentation

- [Solution Design Bundle](../solution-design-bundle/docs/)
- [API Contract (OpenAPI)](../solution-design-bundle/docs/03-api-contract-openapi.yaml)
- [Architecture Overview](../solution-design-bundle/docs/02-architecture-diagrams.md)
