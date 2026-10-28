# KUKA FM Middleware – Implementation Log

Recap of the work done so far and the planned next phases.

| # | Phase | Status |
|---|---|---|
| 1 | Specification | ✅ Done |
| 2 | Backlog | ✅ Done |
| 3 | System design, CLAUDE.md, agents | ✅ Done |
| 4 | Backbone (scaffolding) with agents | ✅ Done |
| 5 | Feature: Robot management | ✅ Done |
| 6 | Feature: Mission management | ✅ Done |
| 7 | Testing and hardening | ⏳ Planned |
| 8 | Deployment via CI/CD | ⏳ Planned |

Workflow used from phase 4 on: **planner → user approves → expert agents implement → reviewer → user reviews.**
From phase 5 on the planner maps every step to backlog **acceptance criteria**, and the reviewer validates them and routes findings to the owning agent.

---

## Phase 1 – Specification ✅

**Input**
- `specification.md` (original, Slovak) – goals, FR1–FR7, NFR1–NFR5, chapters 3–8.
- `KUKA_AMR_Fleet_2.15_en-API.pdf` – chapter 7 (KUKA interface API).

**Key decisions**
- Polling only (`jobQuery`, `robotQuery`); no `missionStateCallback` (reverse proxy).
- `RACK_MOVE` only, cancel `FORCE` only; no pause/resume/retry.
- Track only missions dispatched by the middleware.
- Frontend polls every 1 s; English everywhere.
- KUKA token supplied by CD; token validation is on KUKA FM side.
- NFR2 (own API protection) out of scope – no auth, users or roles.

**Outputs** (`documentation/`)
- Use cases: `use-cases.drawio` – actors Operator, Scheduler, KUKA FM; UC1–UC8 in 2 subsystems.
- Process diagrams: `process-diagrams/uc1…uc8-*.md` (Mermaid sequence diagrams).
- ERD: `erd.drawio` – `robot`, `robot_status_history`, `mission`, `mission_status_history`.
- View mockups: `mockups/views-mockup.html` (Missions + Robots, history, dispatch/cancel, 1 s polling).
- Images: `images/*.png` (exported diagrams + mockup screenshots).
- Updated spec: `documentation/specification.md` with chapters 3–6 filled.

---

## Phase 2 – Backlog ✅

**Structure** (Azure Boards, Agile process)
- 1 Epic → 3 Features (Mission Management, Robot Management, Deployment) → 11 User Stories → 48 Tasks.
- Stories = use cases (UC1–UC8) + Containerize / CI / CD.
- Tasks per use case: backend endpoint, DB, KUKA FM call, frontend, unit tests.

**Columns:** Order No, Work Item Type, Title, Description, Acceptance Criteria (Features + User Stories), Activity (Backend / Frontend / Testing / Deployment), Estimated Hours.

**Later updates:** container code and priority removed from missions; Cancel only for cancelable statuses (10, 20, 25, 50).

**Outputs** (`backlog/`)
- `backlog.csv` – import-ready.
- `backlog.xlsx` – formatted, roll-up formulas, Summary sheet (164 h total).

---

## Phase 3 – System design, CLAUDE.md, agents ✅

**`.claude/system-definition.md`** – *what* to build
- Scope, KUKA FM contract (endpoints, envelope, error and status codes).
- Business rules (sync order, history on every change, `jobQuery` per-jobCode fallback).
- Data model (nullability, FK `Restrict`), REST API contract, frontend views.
- Configuration, containers, CI/CD, testing policy (edge cases first).

**`CLAUDE.md`** – *how* to work (loaded in every session)
- Agents and workflow, folder ownership.
- Rules: ask never assume, stop after each phase, no new dependencies without approval, minimal code.
- Architecture: one API project with feature folders, small repositories over `DbContext`, Angular `core/features/shared`.
- Commands section (filled after phase 4).

**`.claude/agents/`**
| Agent | Role | Edits code |
|---|---|---|
| `planner` | Phase plan mapped to backlog + agents | No |
| `backend-expert` | `backend/` | Yes |
| `frontend-expert` | `frontend/` | Yes |
| `ops-infra-expert` | `docker/`, `ci-cd/` | Yes |
| `reviewer` | Reviews each step, runs builds/tests | No |

---

## Phase 4 – Backbone (scaffolding) with agents ✅

**Plan** – by `planner`; dependencies approved up front.

**Backend** (`backend-expert`)
- `KukaFm.Middleware.slnx`, `global.json` (SDK 10.0.101), `Directory.Build.props`, `nuget.config` (nuget.org only), dotnet-ef tool.
- API: `/api/health`, port 5000, Serilog → Console + Seq (Information).
- Config: `appsettings.json` (no credentials), `.Development.json`, `.Production.json` (placeholders).
- Data: DbContext, 4 entities, `InitialCreate` migration, `Database.Migrate()` on startup.
- Postgres: password auth only (`GSS Encryption Mode=Disable`).
- Test project: xUnit v3 + NSubstitute (no tests yet).

**Frontend** (`frontend-expert`)
- Angular 22, zoneless, plain CSS, strict mode.
- `core/`, `features/missions`, `features/robots`, `shared/`; routes `/missions`, `/robots`.
- No tests, no prettier, no README, no serve target; production build only.

**Ops** (`ops-infra-expert`)
- `api.Dockerfile`, `web.Dockerfile`, `nginx.conf` (`/api/` → api:5000).
- `docker-compose.yml`: web 8080, api internal, postgres 5432, seq 5341; `restart: unless-stopped`.
- `.env` (dev values), `.env.prod` (placeholders); volumes per `ASPNETCORE_ENVIRONMENT`.
- `ci.yml`: no triggers, UseDotNet/UseNode, build, NuGet vuln check, npm audit, tests + trx.
- `cd.yml`: manual; pinned jq 1.8.2 + SHA256; placeholder replacement → compose build/up → smoke check → prune → restore placeholders.

**Review and fixes** (`reviewer`)
- Verdict *changes required* → fixed:
  - PowerShell 5.1 stderr handling in pipelines.
  - `;` rejected in DB values.
  - Separate dev/prod volumes.
  - Boilerplate removed (`launchSettings.json`, Angular serve/dev config).
- Re-check verdict: **approve**.

**Verified**
- `dotnet build` 0 warnings · `dotnet test` exit 0 · `ng build` · `npm audit` 0 vulnerabilities.
- Dev stack up: `:8080` 200, `/api/health` 200, Seq UI, 4 tables migrated.

---

## Phase 5 – Feature: Robot management ✅

**Goal:** robots synced from KUKA FM and visible in UI with history (UC6–UC8).

**Preparation**
- Planner re-run with acceptance-criteria mapping; 12 open questions decided by the user.
- KUKA FM reachability tested from a container: DNS via hosts file works; TLS needs the self-signed cert.
- Self-signed PFX (with private key) deleted; only the public `docker/certs/kukafm.crt` kept; `*.pfx`, `*.key`, `~$*` gitignored.
- ERD, spec and `documentation/specification.md` updated: history tables mirror **every** parent column.

**Key decisions**
- `robotQuery` always `{}` (no filters); only status codes 1–7, no fallback – invalid data fails the whole tick.
- Robots missing from the response stay unchanged; DB errors handled like KUKA errors.
- Robot history DTO includes mapped `state`; newest first, max 50 rows.
- Mission sync (phase 6) waits for the first successful robot sync with ≥ 1 robot – failures cascade by design.
- Failed KUKA calls logged twice on purpose (client Warning + sync Error).
- Same real KUKA FM (`appsettings`) for development and production.

**Backend** (`backend-expert`)
- Migration `MirrorHistoryColumns` (both history tables mirror all parent columns).
- Options `KukaFm`, `Sync.RobotIntervalSeconds`; typed KUKA client (`robotQuery`), one error type, token never logged.
- Status mapping 1–7 → name + Available/Busy/Offline.
- Robot repository, robot sync service + worker (first tick at startup, then interval).
- `GET /api/robots`, `GET /api/robots/{id}/history`; ProblemDetails for all errors (`UseStatusCodePages`).
- 57 unit tests (client errors, mapping, every sync case, service/404).

**Frontend** (`frontend-expert`)
- `core/` models, robots API, ProblemDetails message helper; `shared/` state badge, battery bar.
- Robots view per mockup: Show history (grey rows, valid from – valid to), Auto-refresh 1 s (`exhaustMap`), error banner.

**Ops** (`ops-infra-expert`)
- API image trusts `docker/certs/kukafm.crt` (`update-ca-certificates`), still runs as non-root.

**Review** (`reviewer`)
- Acceptance criteria 1.2, 1.2.1–1.2.3: all met (1.2.3.1 mission-sync signal → phase 6).
- 3 minor findings (ProblemDetails for invalid routes, unused CSS / fallback / field) → fixed; re-check **approve**.

**Verified**
- `dotnet build` 0 warnings · 57/57 tests · no vulnerable packages · `ng build` · `npm audit` 0.
- Dev stack: `/api/robots` 200 `[]`, unknown history 404 ProblemDetails, migrations applied, sync logs KUKA `TIMEOUT`, no token in logs.

**Open**
- KUKA FM down → live sync not verified (moved to phase 8 checks).
- Docker build failed twice before succeeding (BuildKit / `npm ci`), no file changes – watch in CD.
- Header alignment with mockup → phase 6.

**Backlog:** 1.2.1.1–1.2.1.4, 1.2.2.1–1.2.2.4, 1.2.3.1 (except mission-sync signal), 1.2.3.2–1.2.3.4

---

## Phase 6 – Feature: Mission management ✅

**Goal:** dispatch, cancel and track missions with history (UC1–UC5).

**Key decisions**
- `submitMission` sends exactly the verified fields: `orgId` (Labak), GUID `missionCode` / `requestId`, `RACK_MOVE`, robot, start → target node.
- Container code and priority removed everywhere (DB, API, UI, ERD, mockup, backlog).
- Cancel only for 10, 20, 25, 50 – status 28 (Cancelling) → 409.
- Dispatch and cancel return **503 "Synchronization not ready yet"** until robot and mission sync both succeeded once.
- Lists support `includeHistory=true` (newest 100 rows embedded, one request per tick); history endpoints paginated (≤ 100 per page) – for missions and robots.
- Mission sync: one `jobQuery` + per-jobCode fallback; null `robotId` keeps the current robot; unknown robot or status fails the tick.
- Invalid `state` → 400; 201 without `Location` header.

**Backend** (`backend-expert`)
- Migration `RemoveMissionContainerAndPriority`.
- KUKA client: `submitMission`, `missionCancel` (FORCE), `jobQuery` (+ by jobCode).
- Readiness gate (1.2.3.1 signal), mission repository, paging helper.
- Mission service + endpoints (400 field errors, 404, 409, 502, 503); robot endpoints switched to `includeHistory` + paging.
- Mission sync worker with full history rows and `completed_at`.
- `nuget.config` ignores user-level disabled sources.
- 181 unit tests.

**Frontend** (`frontend-expert`)
- Missions view: filter, Show history + "Load older", Auto-refresh 1 s, Cancel for cancelable statuses.
- Dispatch dialog (field errors, 502/503) and Cancel dialog (FORCE), toasts.
- Robots view: one request per tick + "Load older"; header aligned with the mockup.
- `npm audit fix`: Angular build tooling 22.2.5 (9 approved optional packages) → 0 vulnerabilities.

**Documentation**
- Spec, backlog acceptance criteria and tasks, ERD + PNG, mockup + screenshots updated.

**Review** (`reviewer`)
- All acceptance criteria of 1.1, 1.1.1–1.1.5 and 1.2.3.1 met (live KUKA calls not verifiable).
- Minor findings fixed (single `RACK_MOVE` constant, backlog task wording); re-check **approve**.

**Verified**
- `dotnet build` 0 warnings · 181/181 tests · no vulnerable packages · `ng build` · `npm audit` 0.
- Dev stack: all list/history endpoints and 400/404 responses as specified; dispatch and cancel 503 while KUKA FM is down; mission sync waits for robot sync; no token in logs.

**Open**
- KUKA FM down → live mission calls and status 31 after cancel not verified (phase 8).
- `includeHistory` SQL not yet run against real data (phase 7).
- DB save failing after KUKA accepted a dispatch → 500 (phase 7).

**Backlog:** 1.1.1.1–1.1.1.5, 1.1.2.1–1.1.2.5, 1.1.3.1–1.1.3.4, 1.1.4.1–1.1.4.4, 1.1.5.1–1.1.5.4, 1.2.3.1

---

## Phase 7 – Testing and hardening ⏳

- Review edge-case coverage against system-definition §11.
- Run `includeHistory` and paginated history against real data.
- Decide handling of a DB save failure after KUKA accepted a dispatch.
- Full dev stack run: dispatch → sync → cancel → history, verified in UI and Seq.
- Logging check: user operations, KUKA calls, sync results; no secrets in logs.
- Reviewer pass over the whole codebase (minimal code, spec conformity).
- Fix findings; final `docker compose` verification.

---

## Phase 8 – Deployment via CI/CD ⏳

**Prerequisites**
- Git repository in Azure DevOps (user).
- Pipelines created from `ci-cd/ci.yml` and `ci-cd/cd.yml`.
- Pipeline variables: `AgentPoolName`, `AgentName`, `DbName`, `DbUser`, `DbPassword` (secret), `KukaAccessToken` (secret).
- Hosts entry for `kukafm.its.fei` on the agent host (`147.175.126.137`).
- KUKA FM reachable (was down during phase 5).

**Steps**
1. Run CI manually → green build, vuln check, audit, test results published.
2. Verify `$(AgentPoolName)` resolves; adjust if not.
3. Run CD manually → placeholders replaced, stack built and started, smoke check green.
4. Verify production: UI on `:8080`, `/api/health`, Seq logs, robots synced from real KUKA FM.
   - TLS handshake with the trusted self-signed cert succeeds.
   - KUKA field types match (`status`, `batteryLevel` numeric; `robotId`, `code` strings).
   - `/api/robots/abc/history` returns ProblemDetails.
   - Dispatch and cancel work against real KUKA FM; status 31 appears after a cancel; `jobQuery` field types match.
5. Import backlog (`backlog.csv`) into Azure Boards; close completed items.
6. Document the final solution in the Wiki (acceptance criterion).
