# Monitoring and Alerting Plan

## 1. Logging
- Structured JSON logs
- Correlation ID:
  - Accept `X-Correlation-Id` from client or generate per request
- Log levels:
  - Information: normal request lifecycle
  - Warning: validation failures, conversion unavailable
  - Error: upstream failures, DB exceptions

## 2. Metrics (minimum set)
- Request count + latency by route and status code
- Error rate by error code (`VAL-0001`, `FX-4220`, `FX-5030`, etc.)
- FX cache hit rate
- Treasury API call rate + failure rate + latency
- DB query latency (optional)

## 3. Tracing
- OpenTelemetry tracing for:
  - inbound HTTP
  - EF Core operations
  - outbound Treasury API calls

## 4. Health Checks
- `/health/live` — process up
- `/health/ready` — can access DB + basic dependencies

## 5. Alerts (examples)
- 5xx rate > 2% over 5 minutes
- Treasury upstream failures > threshold (possible outage)
- FX cache hit rate drops significantly (unexpected cache invalidation)
- DB file errors / write failures
