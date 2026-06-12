# Containerization Design — Local Dev Environment

**Date:** 2026-06-11
**Status:** Approved (Approach A — full compose: apps + Redis in containers)

## Goal

Provide a one-command local development environment (`docker compose up --build`)
that runs both runnable projects and Redis in containers, while keeping the
existing `dotnet run` / IDE workflow untouched. Production deployment is out of
scope for this phase.

## Context

- .NET 10 Clean Architecture solution (`AreWeDoomd.Api.slnx`).
- Runnable projects:
  - `src/AreWeDoomd.Api` — ASP.NET Core web app (SQL Server via EF Core,
    SignalR hub at `/hubs/agent-notifications`, local port 5188).
  - `src/AreWeDoomd.AgentService` — generic-host worker that connects to the
    Api's SignalR hub and HTTP endpoints, and calls Gemini/Anthropic APIs.
- Database: existing external SQL Server (NOT containerized). Connection
  string key: `ConnectionStrings:AreWeDoomdSql`.
- Redis: planned for future use. The container is provisioned now; no
  application code or configuration consumes it yet.
- More runnable projects are expected later; the structure must make adding
  one cheap (one Dockerfile + one compose service block).

## Files Added

```
src/AreWeDoomd.Api/Dockerfile
src/AreWeDoomd.AgentService/Dockerfile
.dockerignore
docker-compose.yml
.env.example          (committed)
.env                  (git-ignored; add to .gitignore)
```

## Dockerfiles

Multi-stage builds. Build context is the **repo root** so project references
resolve.

- Build stage: `mcr.microsoft.com/dotnet/sdk:10.0`. Copy `*.csproj` files
  first and run `dotnet restore` for layer caching, then copy sources and
  `dotnet publish -c Release`.
- Runtime stage:
  - Api: `mcr.microsoft.com/dotnet/aspnet:10.0` (listens on container port
    8080, the image default).
  - AgentService: `mcr.microsoft.com/dotnet/runtime:10.0` (plain host, not a
    web app). `Prompts/**/*.md` content files flow through publish
    automatically.

`.dockerignore` excludes `bin/`, `obj/`, `.git/`, `.claude/`, `docs/`,
`postman/`, `tests/` build artifacts.

## docker-compose.yml

Three services on the default compose network:

### `api`
- Builds from `src/AreWeDoomd.Api/Dockerfile`, context `.`.
- Ports: `5188:8080`.
- Environment (values sourced from `.env`):
  - `ConnectionStrings__AreWeDoomdSql` — points at the external SQL Server.
    For a SQL Server on the host machine, use `host.docker.internal` as the
    server name.
  - `Jwt__Key`
  - `AgentNotifications__SharedSecret`
  - `Smtp__*` (optional)
  - `ASPNETCORE_ENVIRONMENT=Development`

### `agent-service`
- Builds from `src/AreWeDoomd.AgentService/Dockerfile`, context `.`.
- `depends_on: api`.
- Environment:
  - `AgentNotifications__HubUrl=http://api:8080/hubs/agent-notifications`
  - `AgentNotifications__ApiBaseUrl=http://api:8080`
  - `AgentNotifications__SharedSecret` (same value as the Api's, from `.env`)
  - `ChatProviders__Gemini__ApiKey` / `ChatProviders__Anthropic__ApiKey`
    (from `.env`)
  - `DOTNET_ENVIRONMENT=Development`

### `redis`
- Image `redis:7-alpine`.
- Named volume for persistence.
- Ports: `6379:6379` (exposed to host for tooling/inspection).
- No app configuration references it yet (deliberate — YAGNI until the code
  uses it).

## Configuration & Secrets

- `.env.example` documents every variable the compose file reads, with safe
  placeholder values and a comment per variable.
- `.env` is git-ignored and holds real local values (DB connection string,
  API keys, secrets).
- Container configuration is applied exclusively via environment variables
  (standard `Section__Key` convention), so `appsettings*.json` files stay
  unchanged and IDE runs are unaffected.

## Error Handling / Resilience

- AgentService already has SignalR reconnect/retry logic; `depends_on`
  ordering plus that logic covers the api/agent startup race.
- The Api fails fast on a missing/invalid DB connection string; the required
  variables are documented in `.env.example`.
- Resilience hardening of the AI provider subsystem is a deliberate later
  phase and out of scope here.

## Verification

1. `docker compose up --build` succeeds.
2. Api responds on `http://localhost:5188` (e.g. Scalar/OpenAPI endpoint or a
   known endpoint returns non-5xx).
3. AgentService logs show a successful hub connection to the Api.
4. `docker exec <redis> redis-cli ping` returns `PONG`.
5. `dotnet run` from the IDE still works unchanged.

## Out of Scope

- Production images, registries, CI publishing.
- SQL Server container.
- Wiring Redis into application code.
- Aspire or other orchestrators.
