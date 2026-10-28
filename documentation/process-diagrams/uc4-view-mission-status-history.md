# UC4 – View mission status history (extends UC3)

```mermaid
sequenceDiagram
    actor UI as Web UI (Operator)
    participant MW as Middleware
    participant KUKA as KUKA FM
    participant DB as Database

    UI->>UI: Check Show history
    Note over UI,MW: Newest 100 rows arrive with the UC3 list (includeHistory=true)
    UI->>UI: Render grey rows (valid from – valid to)
    opt Load older
        UI->>MW: GET /api/missions/{missionCode}/history?page=N&pageSize=100
        MW->>DB: SELECT mission history page (newest first)
        DB-->>MW: rows + total count
        MW-->>UI: 200 page / 404 unknown mission
        UI->>UI: Append older rows
    end
```
