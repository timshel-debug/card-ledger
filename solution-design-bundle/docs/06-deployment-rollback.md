# Deployment and Rollback Plan

## 1. Environments
- **Dev**: local run; SQLite file under app folder; auto migrations enabled.
- **Test/CI**: ephemeral SQLite (in-memory or temp file).
- **Prod**: container or VM; SQLite file on persistent volume.

## 2. Configuration
- Environment variables (preferred):
  - `DB__ConnectionString`
  - `FX__BaseUrl` (default: U.S. Treasury Fiscal Data base URL)
  - `FX__TimeoutSeconds`
  - `FX__RetryCount`
  - `FX__CircuitBreakerFailures`
  - `CARD__HashSalt` (required in production)

## 3. Deployment Strategy
**Blue/Green (recommended)**
- Deploy new version alongside old.
- Run smoke tests against new.
- Switch traffic.
- Keep old version for quick revert.

**Rolling**
- Acceptable for single instance if downtime tolerable.

## 4. Database Migration Strategy
- Preferred: run `dotnet ef database update` in deployment pipeline step.
- SQLite migrations are fast; keep migrations backward compatible where possible.

## 5. Rollback
- Application rollback: switch traffic back to previous deployment (blue/green).
- DB rollback: avoid destructive migrations; otherwise restore from backup.
- FX cache table can be truncated without impacting correctness.

## 6. Backups
- Daily backup of SQLite DB file.
- Prioritize `cards` and `purchases` tables; FX cache is regenerable.
