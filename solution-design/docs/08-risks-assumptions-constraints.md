# Risks, Assumptions, and Constraints

## 1. Assumptions
- The Treasury dataset field `record_date` is treated as the effective date for the exchange rate.
- “Specified currency” will be provided as Treasury `country_currency_desc` to avoid ambiguity.
- System does not require end-user authentication (not specified); can be added later.

## 2. Constraints
- Must run without installing external DB/web server.
- Language: C#.
- Non-functional test automation (perf) not required.

## 3. Risk Register

| Risk | Likelihood | Impact | Mitigation |
|---|---:|---:|---|
| Treasury API intermittent failures | Medium | Medium | Timeout+retry+circuit breaker; cache fallback |
| Currency identifier ambiguity (“Dollar”) | High | High | Use `country_currency_desc` as canonical key |
| Storing card numbers creates security exposure | Medium | High | Store hashed only; never plaintext; minimize outputs |
| SQLite concurrency limits under high load | Low | Medium | Keep API stateless; scale reads; consider PostgreSQL swap if needed |
| Date-window interpretation differs from stakeholders | Medium | Medium | Specify inclusive boundary; tests enforce expected behavior |
| Rounding differences cause disputes | Medium | Medium | Use deterministic rounding away from zero; document and test |
