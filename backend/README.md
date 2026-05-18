# Backend

.NET 10 API for ParentAI. Clean Architecture layout (Domain / Application / Infrastructure / Web), MediatR for CQRS, EF Core + Postgres, Redis cache, JWT auth, Swagger UI.

## Prerequisites

- .NET SDK **10.0**
- Docker Desktop (for Postgres + Redis)

## Run

There are two equivalent ways to bring up the stack.

### A. Just the API against dockerized data services (typical local loop)

```bash
# from backend/
docker compose -f docker/docker-compose.yml up -d postgres redis
dotnet run --project src/Web --launch-profile http
```

- API listens on `http://0.0.0.0:5204` (LAN-reachable for devices on the same Wi-Fi).
- Swagger UI: <http://localhost:5204/docs> (also at `/swagger`, redirected).
- OpenAPI JSON: <http://localhost:5204/openapi/v1.json>.
- Root `/` redirects to `/docs`.

### B. Everything in Docker (closer to production)

```bash
docker compose -f docker/docker-compose.yml up --build
```

Brings up the API + Postgres + Redis + pgAdmin (`:5050`). Use this for CI, container-based dev environments, or anything that needs to match production.

### C. Aspire AppHost (orchestrated dev with a dashboard)

```bash
dotnet run --project src/AppHost
```

Aspire dashboard opens automatically with logs, traces, and the live URL list. Postgres and Redis run as Docker containers managed by Aspire. Requires Docker Desktop.

## Configuration

Default connection strings in `src/Web/appsettings.json` already match `docker/docker-compose.yml`:

- Postgres: `Host=localhost;Port=5432;Database=parentai;Username=parentai;Password=parentai_dev_pass`
- Redis: `localhost:6379`

If your local Postgres uses different credentials, override per-developer with user-secrets (kept out of source):

```bash
dotnet user-secrets set "ConnectionStrings:BackendDb" \
  "Host=localhost;Port=5432;Database=parentai;Username=YOUR_USER;Password=YOUR_PASS" \
  --project src/Web
```

The user-secrets store sits in `%APPDATA%\Microsoft\UserSecrets\parentai-backend-web\secrets.json`.

### JWT

`Jwt:SecretKey`, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenExpiryMinutes`, `Jwt:RefreshTokenExpiryDays` in `appsettings.json`. The default secret is **dev-only** — set a real one for any non-local environment.

### LLM (vLLM, OpenAI-compatible)

Defaults sit in `src/Web/appsettings.json` under the `LLM` section. Per-developer secrets (token, custom endpoint) live in `backend/.env` (gitignored).

```bash
cp .env.example .env
# edit .env with your endpoint + token
```

`.env` uses dotenv syntax with `__` as the section separator (standard .NET env-var convention):

```env
LLM__BaseUrl=https://your-vllm-host/v1/
LLM__Token=...
LLM__Model=google/gemma-4-31B-it
LLM__Temperature=0.3
```

Loading order, lowest precedence first:

1. `appsettings.json` — committed defaults / placeholders.
2. `appsettings.{Environment}.json` — environment-specific overrides.
3. `.env` — loaded into the process environment before `WebApplication.CreateBuilder`, then picked up by the standard `AddEnvironmentVariables()` source. **This wins.**
4. Real process environment variables (Docker, k8s, CI) — used as-is, `.env` never clobbers them.

Behavior:

- When `LLM:BaseUrl`, `LLM:Token`, and `LLM:Model` are all set, `IAiClient` talks to the vLLM endpoint using the OpenAI Chat Completions shape, and `IChildProfileExtractor` uses the Gemma extractor.
- When any of those is missing or blank, the backend falls back to `NullAiClient` + the rule-based extractor so the rest of the app still works without an LLM.

## Solution layout

```
backend/
├── src/
│   ├── AppHost/           # .NET Aspire orchestrator
│   ├── ServiceDefaults/   # OpenTelemetry, health, resilience
│   ├── Shared/            # Cross-stack constants (service / connection-string names)
│   ├── Domain/            # Entities, enums, domain events
│   ├── Application/       # CQRS handlers, validators, interfaces
│   ├── Infrastructure/    # EF Core, Identity/JWT, AI client, weather adapter, workers
│   └── Web/               # Minimal-API endpoints, OpenAPI, middleware
├── tests/                 # Unit, integration, functional
└── docker/
    ├── docker-compose.yml
    └── backend.Dockerfile
```

## Tests

```bash
dotnet test
```

Functional tests use `tests/TestAppHost` (Postgres-only, spun up per test session).

## Endpoints (high level)

| Resource | Routes |
|---|---|
| Auth | `POST /auth/{register,login,refresh,logout}` |
| Users | `GET/PUT /users/me/settings` |
| Families | `POST /families`, member invite/role/remove |
| Children | CRUD under `/children`, plus `/sensitivity`, `/notes`, `/routines`, `/extract-from-message` |
| Calendar | CRUD under `/calendar/families/{familyId}/events` and `/calendar/events/{eventId}` |
| Chats | `POST /chats`, `POST/GET /chats/{id}/messages` |
| Weather | `GET /weather/current?locationKey=...` |
| Recommendations | `POST /recommendations/generate/{childId}`, feedback |

Full schema is browsable at <http://localhost:5204/docs>.

## Common issues

- **`28P01 password authentication failed for user "..."`** — your local Postgres rejects the configured password. Either start the docker postgres (`docker compose up -d postgres`) or set user-secrets to your real credentials.
- **`Failed to determine the https port for redirect.`** — was caused by `UseHttpsRedirection` running under the HTTP-only profile. Removed; if it reappears, do `dotnet build-server shutdown` to flush a stale DLL cache.
- **File locks during build** — usually a leftover dotnet language server. `dotnet build-server shutdown` resolves it.
