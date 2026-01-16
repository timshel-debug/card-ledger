# Solution Design Bundle — Card + Purchase Transactions + FX Conversion

Generated: 2026-01-15T11:08:43Z

## What’s in this bundle

- `docs/01-solution-design.md` — end-to-end detailed solution design (architecture, components, NFRs, patterns, SOLID mapping)
- `docs/02-architecture-diagrams.md` — Mermaid diagrams (C4-ish, sequences, data flows)
- `docs/03-api-contract-openapi.yaml` — OpenAPI 3.0 contract for the proposed HTTP API
- `docs/04-data-model.md` — persistence model, constraints, migrations
- `docs/05-testing-validation.md` — test strategy, test cases, acceptance criteria
- `docs/06-deployment-rollback.md` — deployment, rollback, config, environments
- `docs/07-monitoring-alerting.md` — logging, metrics, tracing, SLOs/alerts
- `docs/08-risks-assumptions-constraints.md` — risk register + mitigations
- `docs/09-cost-analysis.md` — engineering + operational cost estimates
- `docs/10-alternatives-considered.md` — alternatives + rationale
- `docs/11-traceability.md` — requirement traceability matrix

## Intended implementation shape (non-code)

The design assumes:
- .NET 8, ASP.NET Core Minimal API, EF Core + SQLite (file-based) for zero external deps.
- Hexagonal / Clean Architecture layering to enforce SOLID and testability.
- External FX rates via U.S. Treasury Fiscal Data API (`/v1/accounting/od/rates_of_exchange`).

This bundle is a design spec, not an implementation.
