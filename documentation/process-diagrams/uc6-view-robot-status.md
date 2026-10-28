# UC6 – View robot status

```mermaid
sequenceDiagram
    actor UI as Web UI (Operator)
    participant MW as Middleware
    participant KUKA as KUKA FM
    participant DB as Database

    UI->>UI: Open Robots
    loop every 1 s while Auto-refresh is on
        UI->>MW: GET /api/robots(?includeHistory=true)
        MW->>DB: SELECT robots
        DB-->>MW: rows (+ newest 100 history rows each)
        MW->>MW: Map status → Available / Busy / Offline
        MW-->>UI: 200 list
        UI->>UI: Render table
    end
```
