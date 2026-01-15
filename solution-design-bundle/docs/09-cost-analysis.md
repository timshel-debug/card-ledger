# Cost Analysis (Estimates)

> These are ballpark estimates; actuals depend on team velocity and “production hardening” expectations.

## 1. Development Cost
Assuming 1 engineer.

| Workstream | Effort |
|---|---:|
| Solution skeleton + CI | 0.5–1 day |
| Domain + persistence + endpoints | 1–2 days |
| FX integration + caching + resilience | 1–2 days |
| Testing + docs + polish | 1–2 days |
| **Total** | **3.5–7 days** |

## 2. Operational Cost
- **Compute**: minimal (single small VM or container)
- **Storage**: SQLite file size grows with purchases; likely small for the exercise
- **Network**: outbound calls to Treasury API, mitigated by caching

## 3. Cost Drivers
- More rigorous security/compliance requirements (e.g., PCI)
- Scaling beyond SQLite’s comfort zone
- High availability across zones (requires different DB choice)

## 4. Cost Optimizations
- Aggressive FX caching (prefetch quarterly rates on schedule)
- Keep service stateless; scale horizontally if needed
