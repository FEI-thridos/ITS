# UC4 – View mission status history (extends UC3)

```mermaid
sequenceDiagram
    actor UI as Web UI (Operator)
    participant MW as Middleware
    participant KUKA as KUKA FM
    participant DB as Database

    UI->>UI: Select mission
    UI->>MW: GET /api/missions/{missionCode}/history
    MW->>DB: SELECT mission history
    DB-->>MW: rows
    MW-->>UI: 200 list
    UI->>UI: Render timeline
```
