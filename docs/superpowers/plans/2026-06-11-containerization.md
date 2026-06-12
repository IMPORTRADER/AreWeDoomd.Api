# Containerization (Local Dev Compose) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One-command local dev environment (`docker compose up --build`) running AreWeDoomd.Api, AreWeDoomd.AgentService, and Redis in containers, against the existing external SQL Server.

**Architecture:** Two multi-stage Dockerfiles (build context = repo root so project references resolve), one root `docker-compose.yml` with three services on the default network, config injected exclusively via environment variables sourced from a git-ignored `.env`. `appsettings*.json` and the IDE workflow stay untouched.

**Tech Stack:** .NET 10 (`mcr.microsoft.com/dotnet/sdk:10.0`, `aspnet:10.0`, `runtime:10.0`), Docker Compose, `redis:7-alpine`.

**Spec:** `docs/superpowers/specs/2026-06-11-containerization-design.md`

---

## Facts the implementer needs (verified against the codebase)

- Project reference graph:
  - `AreWeDoomd.Api` → `Application`, `Infrastructure`, `ActivityNotifications.Contracts`; `Application` → `Domain`, `ActivityNotifications.Contracts`; `Infrastructure` → `Application`.
  - `AreWeDoomd.AgentService` → `ActivityNotifications.Contracts` only.
- The Api is a `WebApplication` (reads `ASPNETCORE_ENVIRONMENT`); .NET 8+ `aspnet` images listen on port **8080** by default. `UseHttpsRedirection()` no-ops with a warning when no HTTPS port is configured — acceptable for dev.
- The Api throws at startup if `AgentNotifications:SharedSecret` is missing (`src/AreWeDoomd.Api/Program.cs:89`).
- Scalar UI + OpenAPI are mapped only in `Development` (`Program.cs:95-102`) — used for verification.
- The AgentService is a generic host (`Host.CreateApplicationBuilder`, reads `DOTNET_ENVIRONMENT`), assembly `AreWeDoomd.AgentService.dll`. It already maps a raw `GEMINI_API_KEY` env var onto `ChatProviders:Gemini:ApiKey` (`src/AreWeDoomd.AgentService/Program.cs:20-27`).
- AgentService's `Prompts/**/*.md` are `Content` items with `CopyToOutputDirectory` — they flow into the publish output automatically.
- SQL connection string key: `ConnectionStrings:AreWeDoomdSql` (`src/AreWeDoomd.Infrastructure/DependencyInjection.cs:51`). **Containers cannot use Windows/Trusted authentication** — the `.env` connection string must use SQL auth and reach the host DB via `host.docker.internal`.
- Docker Desktop must be running on this Windows machine. Check with `docker version` before starting; if the daemon is down, ask the user to start Docker Desktop.

## File Structure

```
.dockerignore                                 (create)
.gitignore                                    (modify: ignore .env)
src/AreWeDoomd.Api/Dockerfile                 (create)
src/AreWeDoomd.AgentService/Dockerfile        (create)
docker-compose.yml                            (create)
.env.example                                  (create, committed)
.env                                          (create locally, NOT committed)
```

---

### Task 1: Build hygiene — `.dockerignore` and `.gitignore`

**Files:**
- Create: `.dockerignore`
- Modify: `.gitignore` (append at end, after the `/.claude` line)

- [ ] **Step 1: Create `.dockerignore`**

Note: in dockerignore syntax, a bare `*.md` matches only root-level files — it must NOT be written as `**/*.md`, because the AgentService needs its `Prompts/**/*.md` files inside the build context.

```
# Build artifacts
**/bin/
**/obj/

# VCS / tooling / docs — not needed in the image
.git/
.github/
.claude/
.vs/
docs/
postman/
tests/

# Root-level docs only (AgentService Prompts/*.md must stay included)
*.md
*.png

# Local secrets and compose files (config comes via environment variables)
.env
docker-compose.yml
```

- [ ] **Step 2: Append `.env` to `.gitignore`**

Append these lines to the end of `D:\AreWeDoomd.Api\.gitignore`:

```
# Local container environment values (secrets) — see .env.example
.env
```

- [ ] **Step 3: Verify git ignores `.env`**

Run:
```powershell
Set-Content -Path .env -Value "TEST=1" -Encoding utf8; git check-ignore .env; git status --short
```
Expected: `git check-ignore` prints `.env`; `git status` shows only `.gitignore` and `.dockerignore` as changes (no `.env`).

- [ ] **Step 4: Commit**

```powershell
git add .gitignore .dockerignore; git commit -m "chore: add .dockerignore and git-ignore .env"
```

---

### Task 2: Api Dockerfile

**Files:**
- Create: `src/AreWeDoomd.Api/Dockerfile`

- [ ] **Step 1: Write the Dockerfile**

```dockerfile
# Build context must be the repository root:
#   docker build -f src/AreWeDoomd.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so 'dotnet restore' is layer-cached
COPY src/AreWeDoomd.Api/AreWeDoomd.Api.csproj src/AreWeDoomd.Api/
COPY src/AreWeDoomd.Application/AreWeDoomd.Application.csproj src/AreWeDoomd.Application/
COPY src/AreWeDoomd.Domain/AreWeDoomd.Domain.csproj src/AreWeDoomd.Domain/
COPY src/AreWeDoomd.Infrastructure/AreWeDoomd.Infrastructure.csproj src/AreWeDoomd.Infrastructure/
COPY src/AreWeDoomd.ActivityNotifications.Contracts/AreWeDoomd.ActivityNotifications.Contracts.csproj src/AreWeDoomd.ActivityNotifications.Contracts/
RUN dotnet restore src/AreWeDoomd.Api/AreWeDoomd.Api.csproj

COPY src/ src/
RUN dotnet publish src/AreWeDoomd.Api/AreWeDoomd.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "AreWeDoomd.Api.dll"]
```

- [ ] **Step 2: Build the image (this is the test)**

Run from the repo root:
```powershell
docker build -f src/AreWeDoomd.Api/Dockerfile -t arewedoomd-api:dev .
```
Expected: build succeeds, final line reports the image was tagged `arewedoomd-api:dev`. First run downloads base images (slow); that's normal.

- [ ] **Step 3: Smoke-test the container fails fast for the RIGHT reason**

The Api requires `AgentNotifications:SharedSecret`; without config it must exit with our known startup error (proves the entrypoint and publish output are correct):
```powershell
docker run --rm arewedoomd-api:dev
```
Expected: Serilog console output ending in `Application terminated unexpectedly` with `AgentNotifications:SharedSecret is not configured`. (A missing-DLL or "executable not found" error means the publish/entrypoint is wrong — stop and fix.)

- [ ] **Step 4: Commit**

```powershell
git add src/AreWeDoomd.Api/Dockerfile; git commit -m "feat: add Api Dockerfile (multi-stage, aspnet:10.0)"
```

---

### Task 3: AgentService Dockerfile

**Files:**
- Create: `src/AreWeDoomd.AgentService/Dockerfile`

- [ ] **Step 1: Write the Dockerfile**

```dockerfile
# Build context must be the repository root:
#   docker build -f src/AreWeDoomd.AgentService/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so 'dotnet restore' is layer-cached
COPY src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj src/AreWeDoomd.AgentService/
COPY src/AreWeDoomd.ActivityNotifications.Contracts/AreWeDoomd.ActivityNotifications.Contracts.csproj src/AreWeDoomd.ActivityNotifications.Contracts/
RUN dotnet restore src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj

COPY src/ src/
RUN dotnet publish src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj -c Release -o /app/publish --no-restore

# Plain worker host, not a web app -> runtime image, no EXPOSE
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AreWeDoomd.AgentService.dll"]
```

- [ ] **Step 2: Build the image**

```powershell
docker build -f src/AreWeDoomd.AgentService/Dockerfile -t arewedoomd-agent:dev .
```
Expected: build succeeds, image tagged `arewedoomd-agent:dev`.

- [ ] **Step 3: Verify the Prompts files made it into the image**

```powershell
docker run --rm --entrypoint ls arewedoomd-agent:dev /app/Prompts
```
Expected: lists the `.md` prompt files (same names as `src/AreWeDoomd.AgentService/Prompts/`). Empty output or "No such file or directory" means the `.dockerignore` or publish step is wrong — stop and fix (check that `.dockerignore` uses `*.md`, not `**/*.md`).

- [ ] **Step 4: Commit**

```powershell
git add src/AreWeDoomd.AgentService/Dockerfile; git commit -m "feat: add AgentService Dockerfile (multi-stage, runtime:10.0)"
```

---

### Task 4: Compose file and environment template

**Files:**
- Create: `docker-compose.yml`
- Create: `.env.example`
- Create: `.env` (local only — never committed)

- [ ] **Step 1: Write `docker-compose.yml`**

```yaml
# Local development environment.
#   1. Copy .env.example to .env and fill in values.
#   2. docker compose up --build
# The IDE workflow (dotnet run) is unaffected by this file.
services:
  api:
    build:
      context: .
      dockerfile: src/AreWeDoomd.Api/Dockerfile
    ports:
      - "5188:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__AreWeDoomdSql: ${AREWEDOOMD_SQL_CONNECTION}
      Jwt__Key: ${JWT_KEY}
      AgentNotifications__SharedSecret: ${AGENT_SHARED_SECRET}
      # SMTP is optional for local dev; add Smtp__Host etc. here when needed.

  agent-service:
    build:
      context: .
      dockerfile: src/AreWeDoomd.AgentService/Dockerfile
    depends_on:
      - api
    environment:
      DOTNET_ENVIRONMENT: Development
      # Container-to-container traffic goes via the compose service name.
      AgentNotifications__HubUrl: http://api:8080/hubs/agent-notifications
      AgentNotifications__ApiBaseUrl: http://api:8080
      AgentNotifications__SharedSecret: ${AGENT_SHARED_SECRET}
      GEMINI_API_KEY: ${GEMINI_API_KEY:-}
      ChatProviders__Anthropic__ApiKey: ${ANTHROPIC_API_KEY:-}

  redis:
    # Provisioned ahead of use; no app config points at it yet.
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data

volumes:
  redis-data:
```

- [ ] **Step 2: Write `.env.example`**

```
# Copy this file to .env and fill in real values. .env is git-ignored.

# Connection string to your existing SQL Server (NOT containerized).
# From inside a container, "host.docker.internal" reaches your machine.
# Must use SQL authentication - Windows/Trusted auth does not work from Linux containers.
AREWEDOOMD_SQL_CONNECTION=Server=host.docker.internal,1433;Database=AreWeDoomd;User Id=sa;Password=<your-password>;TrustServerCertificate=True

# JWT signing key (dev value; any sufficiently long string)
JWT_KEY=dev-signing-key-change-this-before-production-12345

# Shared secret between Api and AgentService (must match on both sides;
# compose injects the same value into both containers)
AGENT_SHARED_SECRET=dev-agent-shared-secret-change-me

# Chat provider API keys (leave empty for providers you don't use)
GEMINI_API_KEY=
ANTHROPIC_API_KEY=
```

- [ ] **Step 3: Create the local `.env`**

Copy the template (the user fills in the real DB password/keys; for now real values may already be in user secrets — ask the user for the connection string if `dotnet user-secrets list --project src/AreWeDoomd.Api` doesn't reveal it):
```powershell
Copy-Item .env.example .env
```
Then set `AREWEDOOMD_SQL_CONNECTION` in `.env` to a working SQL-auth connection string for the existing database.

- [ ] **Step 4: Validate compose interpolation**

```powershell
docker compose config
```
Expected: renders the full config with `.env` values substituted; exits 0. Any "variable is not set" warning means `.env` is missing a key.

- [ ] **Step 5: Commit (everything except `.env`)**

```powershell
git add docker-compose.yml .env.example; git commit -m "feat: add docker compose for local dev (api, agent-service, redis)"; git status --short
```
Expected: `git status` does NOT list `.env`.

---

### Task 5: End-to-end verification (spec section "Verification")

No new files — this task proves the whole stack works.

- [ ] **Step 1: Bring the stack up**

```powershell
docker compose up --build -d; docker compose ps
```
Expected: three services (`api`, `agent-service`, `redis`) with status `Up`/`running`.

- [ ] **Step 2: Api responds on localhost:5188**

```powershell
Invoke-WebRequest -Uri http://localhost:5188/openapi/v1.json -UseBasicParsing | Select-Object StatusCode
```
Expected: `StatusCode 200` (OpenAPI is mapped because the container runs in Development).

- [ ] **Step 3: AgentService connected to the hub**

```powershell
docker compose logs agent-service --tail 50
```
Expected: log lines showing a successful SignalR connection to `http://api:8080/hubs/agent-notifications` and no recurring connection-failure loop. (A one-time retry at startup is fine — `depends_on` only orders container start, not app readiness.)

- [ ] **Step 4: Redis answers**

```powershell
docker compose exec redis redis-cli ping
```
Expected output: `PONG`

- [ ] **Step 5: IDE workflow still works (spec verification item 5)**

```powershell
dotnet build AreWeDoomd.Api.slnx
```
Expected: `Build succeeded`. (No source or appsettings files were touched, so this is a sanity check only.)

- [ ] **Step 6: Tear down and final commit (if any fixes were made)**

```powershell
docker compose down
```
If verification required fixes to any committed file, commit them:
```powershell
git add -A; git status --short; git commit -m "fix: adjust container config after end-to-end verification"
```
(Skip the commit if the tree is clean. Confirm `.env` is never staged.)

---

## Self-review notes

- Spec coverage: Dockerfiles (Tasks 2-3), compose with api/agent-service/redis (Task 4), `.dockerignore` + `.gitignore` (Task 1), `.env`/`.env.example` (Task 4), all five spec verification items (Task 5; container fail-fast on missing secret additionally covered in Task 2 Step 3).
- The spec's `docs/` exclusion in `.dockerignore` is safe: no project references files under `docs/`.
- Type/name consistency: image tags `arewedoomd-api:dev` / `arewedoomd-agent:dev` are only used within their own tasks; compose builds its own images independently.
