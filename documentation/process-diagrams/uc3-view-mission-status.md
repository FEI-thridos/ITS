# UC3 – View mission status

```mermaid
sequenceDiagram
    actor UI as Web UI (Operator)
    participant MW as Middleware
    participant KUKA as KUKA FM
    participant DB as Database

    UI->>UI: Open Missions
    loop every 1 s
        UI->>MW: GET /api/missions?state=active|finished
        MW->>DB: SELECT missions (filter)
        DB-->>MW: rows
        MW-->>UI: 200 list
        UI->>UI: Render table
    end
```
