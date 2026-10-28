## 1. Ciele a stav

- **Súčasný stav:** KUKA AMR Fleet Manager (KUKA FM) beží samostatne, bez napojenia na okolité systémy (ERP/PLM/WMS) — obsluha musí missie zadávať/sledovať priamo v jeho UI.
- **Cieľový stav:** middleware sprostredkuje riadenie a sledovanie misií pre iný (fiktívny) systém — zjednodušuje a zjednocuje prístup k funkciám KUKA FM cez vlastné REST rozhranie a webové UI.

## 2. Požiadavky

## Funkcionálne požiadavky (FR)

| ID | Požiadavka |
|---|---|
| FR1 | Middleware umožní zadať (dispatch) novú misiu robotovi cez vlastný endpoint, ktorý interne zavolá `submitMission` na KUKA FM. |
| FR2 | Middleware umožní zrušiť (cancel) rozbehnutú misiu cez vlastný endpoint, ktorý interne zavolá `cancelMission` na KUKA FM. |
| FR3 | Middleware periodicky (pollingom) zisťuje aktuálny stav aktívnych misií cez Query jobs API KUKA FM a ukladá ho do vlastnej databázy. |
| FR4 | Middleware periodicky zisťuje aktuálny stav robotov (dostupný/zaneprázdnený/offline) cez príslušné query API KUKA FM a ukladá ho do vlastnej databázy. |
| FR5 | Middleware uchováva históriu stavov jednotlivých misií (nie len posledný stav) pre základné reportovanie. |
| FR6 | Middleware poskytuje webové rozhranie (frontend) so zoznamom misií a ich aktuálnym/historickým stavom. |
| FR7 | Middleware poskytuje webové rozhranie so zoznamom robotov a ich aktuálnym stavom. |

## Nefunkcionálne požiadavky (NFR)

| ID | Požiadavka |
|---|---|
| NFR1 | Middleware je kontajnerizovaný a používa perzistentnú databázu. |
| NFR2 | Vlastné API middlewaru je chránené — neautorizovaný/neplatný request je odmietnutý. |
| NFR3 | Middleware overuje platnosť KUKA FM `Authorization` tokenu pri každom volaní na KUKA FM (nutné od úplného začiatku vývoja, inak žiadne volanie nefunguje). |
| NFR4 | Middleware zapisuje štruktúrované logy o vlastnej činnosti a chybách. |
| NFR5 | Middleware je nasaditeľný cez CI/CD pipeline (build, test, statická analýza, redeploy). |

## 3. Prípady použitia (use-case diagram)

Source: [use-cases.drawio](use-cases.drawio)

![Use-case diagram](images/use-case-diagram.png)

**Actors**
- **Operator** – user of the web UI (no users/roles on middleware side).
- **Scheduler** (timer) – backend background jobs polling KUKA FM.
- **KUKA FM** (external system) – KUKA AMR Fleet Manager at `https://kukafm.its.fei`.

**Subsystems:** Mission Management (UC1–UC5), Robot Management (UC6–UC8).

| ID | Use case | Actor | Goal | Main scenario |
|---|---|---|---|---|
| UC1 | Dispatch mission | Operator | Send a new RACK_MOVE mission to KUKA FM. | Operator fills robot, start node and target node → system checks that syncs are ready (else 503) and validates input (else 400, no KUKA call) → calls KUKA `submitMission` → stores mission (10 Created) → 201. *Alt:* KUKA error → 502, shown and logged; mission is not stored. |
| UC2 | Cancel mission | Operator | Stop a running mission. | Operator selects a cancelable mission (10, 20, 25, 50) and confirms → system calls KUKA `missionCancel` (FORCE) → logs and confirms. Final status *Cancelled* is stored by UC5. *Alt:* syncs not ready → 503; unknown → 404; 28 Cancelling or final → 409; KUKA error → 502. |
| UC3 | View mission status | Operator | See current status of active and finished missions. | Operator opens Missions → system shows missions with current status (filter All / Active / Finished) → list refreshes every 1 s. |
| UC4 | View mission status history (extends UC3) | Operator | See all previous statuses of a mission. | Operator enables *Show history* → newest 100 previous states come with the list; *Load older* pages through the rest. |
| UC5 | Synchronize mission status | Scheduler | Keep stored mission statuses up to date. | Starts after the first successful robot sync (≥ 1 robot). On interval loads active missions (10, 20, 25, 28, 50) → one KUKA `jobQuery` + per-jobCode fallback for missing ones → for changed missions stores previous state in history and updates; final status sets `completed_at`. Final statuses (30, 31, 35, 60) are no longer polled. |
| UC6 | View robot status | Operator | See current state of all robots. | Operator opens Robots → system shows robots with state Available / Busy / Offline, KUKA status, battery, node and mission → list refreshes every 1 s. |
| UC7 | View robot status history (extends UC6) | Operator | See all previous states of a robot. | Operator enables *Show history* → newest 100 previous states come with the list; *Load older* pages through the rest. |
| UC8 | Synchronize robot status | Scheduler | Keep stored robot states up to date. | First on startup, then on interval system calls KUKA `robotQuery` → inserts new robots, for changed robots stores previous state in history and updates; first success with ≥ 1 robot signals mission sync to start. |

## 4. Procesný diagram / diagram aktivít

Sources: [process-diagrams/](process-diagrams/)

Participants: Web UI (Operator) or Scheduler, Middleware, KUKA FM, Database. View use cases read only from the database; KUKA FM is queried only by the scheduler jobs. Robot sync runs first on startup, then mission sync.

### 4.1 UC1 – Dispatch mission

```mermaid
sequenceDiagram
    actor UI as Web UI (Operator)
    participant MW as Middleware
    participant KUKA as KUKA FM
    participant DB as Database

    UI->>UI: Fill robot, start node, target node + Dispatch
    UI->>MW: POST /api/missions
    alt syncs not ready yet
        MW-->>UI: 503 Synchronization not ready yet
    else invalid input (required, max 64 chars, robot exists)
        MW-->>UI: 400 field errors (no KUKA call)
    else valid
        MW->>MW: Generate missionCode + requestId (GUID)
        MW->>KUKA: submitMission (RACK_MOVE)
        KUKA-->>MW: success / error
        alt success
            MW->>DB: INSERT mission (10 Created)
            MW->>MW: Log to Seq
            MW-->>UI: 201 Mission
        else KUKA error
            MW->>MW: Log error to Seq
            MW-->>UI: 502 KUKA FM error (nothing stored)
        end
    end
    UI->>UI: Show result (toast / error in dialog)
```

### 4.2 UC2 – Cancel mission

```mermaid
sequenceDiagram
    actor UI as Web UI (Operator)
    participant MW as Middleware
    participant KUKA as KUKA FM
    participant DB as Database

    UI->>UI: Cancel + confirm (FORCE)
    UI->>MW: POST /api/missions/{missionCode}/cancel
    alt syncs not ready yet
        MW-->>UI: 503 Synchronization not ready yet
    else
        MW->>DB: SELECT mission
        DB-->>MW: mission
        alt unknown mission
            MW-->>UI: 404
        else not cancelable (28 Cancelling or final)
            MW-->>UI: 409
        else cancelable (10, 20, 25, 50)
            MW->>KUKA: missionCancel (cancelMode = FORCE)
            KUKA-->>MW: success / error
            MW->>MW: Log to Seq
            MW-->>UI: 200 / 502 KUKA FM error
        end
    end
    UI->>UI: Show result
    Note over MW,DB: Local status is not changed – Cancelled (31) is stored by UC5 sync
```

### 4.3 UC3 – View mission status

```mermaid
sequenceDiagram
    actor UI as Web UI (Operator)
    participant MW as Middleware
    participant KUKA as KUKA FM
    participant DB as Database

    UI->>UI: Open Missions, choose All / Active / Finished
    loop every 1 s while Auto-refresh is on
        UI->>MW: GET /api/missions?state=active|finished(&includeHistory=true)
        alt invalid state
            MW-->>UI: 400
        else
            MW->>DB: SELECT missions (filter, newest first)
            DB-->>MW: rows (+ newest 100 history rows each)
            MW-->>UI: 200 list
            UI->>UI: Render table
        end
    end
```

### 4.4 UC4 – View mission status history

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

### 4.5 UC5 – Synchronize mission status

```mermaid
sequenceDiagram
    participant SCH as Scheduler
    participant MW as Middleware
    participant KUKA as KUKA FM
    participant DB as Database

    Note over SCH: Starts after first successful robot sync (≥ 1 robot), then interval from appsettings
    SCH->>MW: Timer tick – run mission sync
    MW->>DB: SELECT active missions (10, 20, 25, 28, 50)
    DB-->>MW: missions
    alt no active missions
        MW->>MW: Nothing to do (tick counts as successful)
    else
        MW->>KUKA: jobQuery (limit + filters)
        KUKA-->>MW: jobs / error
        loop for each active mission missing in the response
            MW->>KUKA: jobQuery (jobCode)
            KUKA-->>MW: job / nothing / error
        end
        alt KUKA / DB error or invalid data
            MW->>MW: Log error, write nothing, retry next tick
        else
            loop for each mission whose status or robot changed
                MW->>DB: INSERT previous state into history (all columns)
                MW->>DB: UPDATE mission (+ completed_at if final)
            end
            MW->>MW: Log counts to Seq, mark missions ready
        end
    end
    Note over MW,DB: Missions in 30, 31, 35, 60 are final – not polled anymore
```

### 4.6 UC6 – View robot status

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

### 4.7 UC7 – View robot status history

```mermaid
sequenceDiagram
    actor UI as Web UI (Operator)
    participant MW as Middleware
    participant KUKA as KUKA FM
    participant DB as Database

    UI->>UI: Check Show history
    Note over UI,MW: Newest 100 rows arrive with the UC6 list (includeHistory=true)
    UI->>UI: Render grey rows (valid from – valid to)
    opt Load older
        UI->>MW: GET /api/robots/{id}/history?page=N&pageSize=100
        MW->>DB: SELECT robot history page (newest first)
        DB-->>MW: rows + total count
        MW-->>UI: 200 page / 404 unknown robot
        UI->>UI: Append older rows
    end
```

### 4.8 UC8 – Synchronize robot status

```mermaid
sequenceDiagram
    participant SCH as Scheduler
    participant MW as Middleware
    participant KUKA as KUKA FM
    participant DB as Database

    Note over SCH: First tick at startup, then interval from appsettings
    SCH->>MW: Timer tick – run robot sync
    MW->>KUKA: robotQuery {} (all robots)
    KUKA-->>MW: robots / error
    alt KUKA / DB error or invalid data
        MW->>MW: Log error, write nothing, retry next tick
    else success
        MW->>MW: Map status (Available / Busy / Offline)
        MW->>DB: SELECT robots
        DB-->>MW: stored robots
        loop for each robot from KUKA
            alt new robot
                MW->>DB: INSERT robot
            else any column changed
                MW->>DB: INSERT previous state into history (all columns)
                MW->>DB: UPDATE robot
            end
        end
        MW->>MW: Log counts to Seq
        opt first success with ≥ 1 robot
            MW->>MW: Signal mission sync to start (robots ready)
        end
    end
```

## 5. Dátový model (ERD)

Source: [erd.drawio](erd.drawio)

![ERD](images/erd.png)

| Entity | Purpose |
|---|---|
| `robot` | Current state of each robot from `robotQuery` (`robot_code` = KUKA `robotId`). |
| `robot_status_history` | Previous states of a robot – mirrors every `robot` column, one row per change (`valid_from` → `valid_to`). |
| `mission` | Missions dispatched by the middleware; current status from `jobQuery`. |
| `mission_status_history` | Previous states of a mission – mirrors every `mission` column, one row per change. |

**Relations:** 1 robot → 0..* missions (each mission has exactly 1 robot, `robot_id` NOT NULL) · 1 robot → 0..* robot history rows · 1 mission → 0..* mission history rows · 1 robot → 0..* mission history rows. All FKs `ON DELETE RESTRICT`.

**Rules:**
- History tables mirror every column of their parent table (except `id`; `updated_at` becomes `valid_from`); unique keys are not copied.
- Every change stores the previous state in the history table.
- Columns are NOT NULL unless marked NULL (nullable: values KUKA may not return, `robot.mission_code`, `mission.completed_at`).
- Available / Busy / Offline is mapped from `robot.status` in code (Idle → Available; Departure, Executing, Charging, Updating → Busy; Offline, Abnormal → Offline).

## 6. Návrh view aplikácie

Source (interactive mockup): [mockups/views-mockup.html](mockups/views-mockup.html)

Both views poll the backend every 1 s (*Auto-refresh*) and can show historical rows (*Show history*).

### 6.1 Missions – list with status and history

![Missions view](images/mockup-missions.png)

### 6.2 Missions – dispatch new mission

![Dispatch mission dialog](images/mockup-dispatch.png)

### 6.3 Missions – cancel running mission

![Cancel mission dialog](images/mockup-cancel.png)

### 6.4 Robots – list with state and history

![Robots view](images/mockup-robots.png)

## 7. Technológie

Použité technológie pri vývoji middlewaru, napr.:
  - Backend: ASP.NET Core minimal API (C#), Entity Framework Core.
  - DB: PostgreSQL.
  - Frontend: jednoduchý web (napr. Blazor, React alebo aj čistý HTML/JS — podľa preferencie tímu).
  - Kontajnerizácia: Docker + docker-compose.

## 8. Akceptačné kritériá

- [ ] Je možné zadať novú misiu.
- [ ] Je možné zrušiť prebiehajúcu misiu.
- [ ] Je možné prezretie stavu prebiehajúcich, ktoré sa aktualizujú v reálnom čase, a ukončených misií.
- [ ] Je možné prezretie stavu robotov, ktoré sa aktualizujú v reálnom čase.
- [ ] Prístup ku KUKA FM API volaniu je zabezpečený access tokenom.
- [ ] Všetky operácie vykonané používateľom sú logované.
- [ ] Proces testovania a nasadenia je plne automatizovaným využitím CI/CD pipeline.
- [ ] Finálne riešenie je kompletne zdokumentované vo Wiki.