# KUKA FM Middleware – Implementation Log

Recap of the work done so far and the planned next phases.

| # | Phase | Status |
|---|---|---|
| 1 | Specification | ✅ Done |
| 2 | Backlog | ✅ Done |
| 3 | System design, CLAUDE.md, agents | ✅ Done |
| 4 | Backbone (scaffolding) with agents | ✅ Done |
| 5 | Feature: Robot management | ⏳ Planned |
| 6 | Feature: Mission management | ⏳ Planned |
| 7 | Testing and hardening | ⏳ Planned |
| 8 | Deployment via CI/CD | ⏳ Planned |

Workflow used from phase 4 on: **planner → user approves → expert agents implement → reviewer → user reviews.**

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

**Columns:** Order No, Work Item Type, Title, Description, Activity (Backend / Frontend / Testing / Deployment), Estimated Hours.

**Outputs** (`backlog/`)
- `backlog.csv` – import-ready.
- `backlog.xlsx` – formatted, roll-up formulas, Summary sheet (164 h total).

**Later adjustments**
- Unit tests only (no integration/E2E, no frontend tests).
- `cd.yml` manual run only.
- Task 1.3.3.4 changed to backend task "Apply DB migrations on API startup".

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

**Quality pass**
- `/code-review` of agent specs → 9 findings, 8 fixed (duplicated rules removed, migration ownership, Seq ports, dev credentials).

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

**Open**
- Pool variable `$(AgentPoolName)` resolution – check on first pipeline run.
- Unused Docker volumes from earlier runs – remove manually if wanted.

---

## Phase 5 – Feature: Robot management ⏳

**Goal:** robots synced from KUKA FM and visible in UI with history (UC6–UC8).

**Backend**
- KUKA client: typed HttpClient, token header, envelope + error handling, `robotQuery`.
- Options classes (`KukaFm`, `Sync`).
- Robot sync background service (runs first on startup), insert / change detection / history.
- Status mapping Available / Busy / Offline.
- `GET /api/robots`, `GET /api/robots/{id}/history`.
- Unit tests: status mapping 1–7, new / unchanged / changed robot, KUKA failures.

**Frontend**
- API service + models, Robots view (state badge, battery bar), Show history, Auto-refresh 1 s.

**Backlog:** 1.2.1.x, 1.2.2.x, 1.2.3.x

---

## Phase 6 – Feature: Mission management ⏳

**Goal:** dispatch, cancel and track missions with history (UC1–UC5).

**Backend**
- `submitMission`, `missionCancel`, `jobQuery` (+ per-jobCode fallback).
- Mission sync (starts after first robot sync), history, `completed_at` on final status.
- Generated `missionCode` / `requestId` (≤ 32 chars, unique).
- `GET /api/missions`, `GET /api/missions/{code}/history`, `POST /api/missions`, `POST /api/missions/{code}/cancel`.
- Unit tests: validation boundaries, cancel 404/409, sync transitions, KUKA errors.

**Frontend**
- Missions view, All/Active/Finished filter, Show history, Auto-refresh 1 s.
- Dispatch dialog (robot select, nodes, container, priority 1–99), Cancel with FORCE confirmation.
- ProblemDetails error messages.

**Backlog:** 1.1.1.x – 1.1.5.x

---

## Phase 7 – Testing and hardening ⏳

- Review edge-case coverage against system-definition §11.
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
- Hosts entry for `kukafm.its.fei` on the agent host.

**Steps**
1. Run CI manually → green build, vuln check, audit, test results published.
2. Verify `$(AgentPoolName)` resolves; adjust if not.
3. Run CD manually → placeholders replaced, stack built and started, smoke check green.
4. Verify production: UI on `:8080`, `/api/health`, Seq logs, robots synced from real KUKA FM.
5. Import backlog (`backlog.csv`) into Azure Boards; close completed items.
6. Document the final solution in the Wiki (acceptance criterion).
