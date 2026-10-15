# KUKA FM Middleware

Middleware between an upstream system and KUKA AMR Fleet Manager (`https://kukafm.its.fei`):
dispatch / force-cancel RACK_MOVE missions, poll mission and robot states into PostgreSQL with full history,
Angular web UI polling every 1 s. Fully containerized, deployed by Azure DevOps pipelines to local Docker.

**Spec (what to build):** `.claude/system-definition.md` – read it before implementing anything. It is binding.
**This file (how to work):** global rules for every session and every agent. Rules live only here – agent files and the system definition do not repeat them.

---

## Agents and workflow

| Agent | Scope | Edits code |
|---|---|---|
| `planner` | Step-by-step plan per phase, mapped to backlog items and agents | No |
| `backend-expert` | `backend/` – ASP.NET Core minimal API, EF Core + migrations, KUKA client, sync jobs, unit tests | Yes |
| `frontend-expert` | `frontend/` – Angular UI | Yes |
| `ops-infra-expert` | `docker/`, `ci-cd/` – Dockerfiles, nginx, compose, CI/CD YAML | Yes |
| `reviewer` | Reviews each step before a phase is closed | No |
| – | `documentation/`, `backlog/`, root files (`CLAUDE.md`, `.gitignore`) – change only when the user asks | – |

Per phase: **planner → user approves plan → expert(s) implement → reviewer → user reviews.**
- The reviewer always validates the **Acceptance Criteria** (`backlog/backlog.csv`) of the User Stories in scope and groups findings by owning agent.
- Findings are routed by the main session: each owner section is sent to that expert agent to fix, then the reviewer re-checks. Findings owned by `user` are asked to the user. (Subagents cannot message each other directly.)
Stay inside your folder. Changes needed elsewhere go through the user.

## Working rules

- **Ask, never assume.** Anything not defined in this file or the system definition → ask the user.
- **Stop after each phase** and let the user review before continuing.
- **English** everywhere: code, comments, logs, UI, docs.
- **No new dependencies** (NuGet, npm, Docker base images, pipeline tasks/extensions) without the user's prior approval – propose name + reason + version first. This also applies to the initial scaffolding.
- **Keep the codebase small and clean:** implement only what the spec and the current backlog item need. No speculative abstractions, generic helpers "for later", unused code, commented-out code or boilerplate the framework already provides. Prefer deleting over adding.
- Every summary lists the backlog items covered (`Order No` from `backlog/backlog.xlsx`).
- Never commit or log secrets (KUKA token, DB credentials).
- Agents only change files; nobody runs the app locally (`dotnet run` / `ng serve` not needed). Verify via build, tests, audits and docker compose.

## Architecture rules

### Backend (`backend/`)
- Solution `backend/KukaFm.Middleware.slnx`; `global.json`, `Directory.*.props` and the dotnet tool manifest (`.config/dotnet-tools.json`) also live in `backend/`.
- **One API project with feature folders**, plus one unit-test project:
  ```
  backend/src/KukaFm.Middleware.Api/
    Missions/   endpoints, DTOs, service, validation
    Robots/     endpoints, DTOs, service, status mapping
    Kuka/       typed HttpClient, request/response models, options
    Sync/       background services (robot sync → mission sync)
    Data/       DbContext, entities, configurations, migrations, repositories
  backend/tests/KukaFm.Middleware.Api.Tests/   mirrors the feature folders
  ```
- **Data access:** small repository interfaces per aggregate (e.g. `IMissionRepository`, `IRobotRepository`) implemented with `DbContext`. Services depend on interfaces only, so they can be unit-tested with NSubstitute.
- Also behind interfaces: KUKA client, time (`TimeProvider`).
- Minimal API endpoints are thin: validate → call service → map result to HTTP status / ProblemDetails.
- **Migrations:** created by `backend-expert` (EF Core), applied by the API on startup (`Database.Migrate()`). No migration step in pipelines.

### Frontend (`frontend/`)
Angular project `kuka-fm-middleware-web`, latest stable Angular, plain CSS.
```
src/app/
  core/       API services, models (DTOs from system-definition §7), HTTP setup
  features/
    missions/ missions view, dispatch dialog, cancel dialog
    robots/   robots view
  shared/     reusable UI (status badges, dialogs, battery bar, …)
```

### Cross-cutting
- **API contract** in system-definition §7 is binding for backend and frontend. Any change (path, DTO field, status code) needs the user's approval first and must be updated in the system definition.
- **Config:** every non-user KUKA FM parameter comes from `appsettings` (system-definition §9) – never hardcoded.
- **Secrets:** real values only in pipeline variables (marked secret). The repo holds **placeholders only** in `appsettings.Production.json` and `docker/.env.prod`; CD replaces them just before building images, so images with real values exist only on the deployment host. Dev DB credentials (`root`/`password`) only in `appsettings.Development.json` / compose defaults.
- **Testing:** backend unit tests only (xUnit + NSubstitute, plain `Assert`, no FluentAssertions). No integration, E2E or frontend tests. Focus on **edge cases**, not just the happy path (see system-definition §11).

## Commands

Verify changes the same way CI does. Nobody runs the app outside Docker.

**Backend** (run in `backend/`)
```
dotnet tool restore
dotnet build KukaFm.Middleware.slnx
dotnet test KukaFm.Middleware.slnx
dotnet list KukaFm.Middleware.slnx package --vulnerable --include-transitive
dotnet ef migrations add <Name> --project src/KukaFm.Middleware.Api --output-dir Data/Migrations
```

**Frontend** (run in `frontend/`)
```
npm ci
npx ng build --configuration production
npm audit --audit-level=high
```

**Docker** (run in repo root)
```
docker compose -f docker/docker-compose.yml up -d --build                        # dev stack (docker/.env)
docker compose -f docker/docker-compose.yml config                               # validate dev
docker compose -f docker/docker-compose.yml --env-file docker/.env.prod config   # validate prod
docker compose -f docker/docker-compose.yml logs api                             # API logs
docker compose -f docker/docker-compose.yml down                                 # stop, keep volumes
docker builder prune -af                                                         # clean build cache
```

**Dev URLs:** app http://localhost:8080 · API http://localhost:8080/api (health: `/api/health`) · Seq http://localhost:5341
