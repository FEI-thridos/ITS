# KUKA FM Middleware

## Main idea

**KUKA AMR Fleet Manager (KUKA FM)** controls autonomous mobile robots, but it runs standalone – operators have to dispatch and watch missions directly in its own UI.

**KUKA FM Middleware** sits between an upstream system and KUKA FM and offers one simple, unified access point:

- **Dispatch missions** – send a new `RACK_MOVE` mission to a chosen robot (KUKA `submitMission`).
- **Cancel missions** – force-cancel a running mission (KUKA `missionCancel`).
- **Track missions and robots** – background jobs poll KUKA FM (`jobQuery`, `robotQuery`) and store the current state plus **full history** of every change in PostgreSQL.
- **Web UI** – Missions and Robots views with current and historical states, refreshed every second.

| Part | Technology |
|---|---|
| Backend | ASP.NET Core minimal API (.NET 10), EF Core + PostgreSQL |
| Frontend | Angular 22, served by nginx |
| Logging | Serilog → Seq + Console |
| Runtime | Docker Compose (web, api, postgres, seq) |
| CI/CD | Azure DevOps pipelines on a local Windows agent, deploying to local Docker |

---

## Project structure

```
kuka-fm-middleware/
├── README.md               # this file
├── implementation.md       # implementation log: all phases, decisions and results
├── CLAUDE.md               # how to work: agents, rules, architecture, commands
├── .claude/
│   ├── system-definition.md   # what to build: binding spec (KUKA contract, rules, data model, API)
│   └── agents/                # planner, backend-expert, frontend-expert, ops-infra-expert, reviewer
├── backend/
│   ├── KukaFm.Middleware.slnx, global.json, Directory.Build.props, nuget.config
│   ├── src/KukaFm.Middleware.Api/
│   │   ├── Missions/        # mission endpoints, DTOs, service
│   │   ├── Robots/          # robot endpoints, DTOs, service, status mapping
│   │   ├── Kuka/            # typed KUKA FM client, options, models
│   │   ├── Sync/            # background sync jobs (robots → missions)
│   │   └── Data/            # DbContext, entities, repositories, migrations
│   └── tests/KukaFm.Middleware.Api.Tests/   # xUnit + NSubstitute unit tests
├── frontend/                # Angular app `kuka-fm-middleware-web`
│   └── src/app/
│       ├── core/            # API services, models, error helper
│       ├── features/        # missions/, robots/ views
│       └── shared/          # reusable UI (state badge, battery bar, …)
├── docker/
│   ├── api.Dockerfile, web.Dockerfile, nginx.conf
│   ├── docker-compose.yml
│   ├── .env                 # development values
│   ├── .env.prod            # production placeholders (filled by CD)
│   ├── certs/kukafm.crt     # public cert of the self-signed KUKA FM server
│   └── dev/seed-dev-data.sql # dev-only sample data (never in images or production)
├── ci-cd/
│   ├── ci.yml               # npm + NuGet checks, unit tests with coverage, SonarQube
│   ├── ci-no-sonar.yml      # same checks and unit tests, without SonarQube
│   └── cd.yml               # manual deployment: copy to DeployPath, fill secrets, compose build/up
├── backlog/                 # backlog.csv / backlog.xlsx for Azure Boards
└── documentation/
    ├── specification.md     # specification with use cases, process diagrams, ERD, views
    ├── use-cases.drawio, erd.drawio
    ├── process-diagrams/    # Mermaid sequence diagrams UC1–UC8
    ├── mockups/             # HTML view mockups
    ├── images/              # exported diagrams and mockup screenshots
    └── data/                # original specification and KUKA FM API documentation
```

Build, test and run commands are listed in [CLAUDE.md → Commands](CLAUDE.md#commands).

---

## Implementation log

[implementation.md](implementation.md) records how the project was built, phase by phase – with the decisions and results of each phase:

| # | Chapter | In short |
|---|---|---|
| 1 | Specification | Scope decisions; use-case, process, ERD and view documentation. |
| 2 | Backlog | Azure Boards structure (Epic → Features → User Stories → Tasks) with acceptance criteria and estimates. |
| 3 | System design, CLAUDE.md, agents | Binding spec, working rules, architecture and the five Claude Code agents. |
| 4 | Backbone (scaffolding) | Solution, Angular app, Docker stack and CI/CD pipelines built and reviewed by the agents. |
| 5 | Feature: Robot management | KUKA client, robot sync with history, robots API and view (history embedded + paginated). |
| 6 | Feature: Mission management | Dispatch, cancel (readiness gate, field validation), mission sync with history and the missions view. |
| 7 | Testing and hardening | Dispatch hardening, extra edge-case tests, seeded and live read-only checks, full review. |
| 8 | Deployment via CI/CD | Two manual pipelines (CI with SonarQube, CD to a dedicated folder) running successfully; live checks against KUKA FM done. |
