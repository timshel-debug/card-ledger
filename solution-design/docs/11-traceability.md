# Traceability Matrix

| Requirement | Design Element(s) | API Endpoint(s) | Data Model | Tests |
|---|---|---|---|---|
| Req #1 Create a Card | Card aggregate, CreateCard UC, CardNumber VO | POST /cards | cards table | AC-01 + unit tests |
| Req #2 Store Purchase | Purchase entity, CreatePurchase UC | POST /cards/{cardId}/purchases | purchases table | AC-02 + integration |
| Req #3 Retrieve Purchase Converted | FxRateResolver, Treasury adapter, caching | GET /cards/{cardId}/purchases/{purchaseId} | fx_rate_cache | AC-03 + contract tests |
| Req #4 Retrieve Available Balance | Balance query + conversion | GET /cards/{cardId}/balance | cards+purchases+fx_rate_cache | AC-04 + integration |
