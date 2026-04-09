# SeismicFlow

Multi-tenant seismic data ingestion and streaming platform built with ASP.NET Core 10.

Devices publish seismic readings over MQTT → the server persists them to PostgreSQL and pushes them to connected clients in real time via SSE.

---



### Project structure

| Project | Role |
|---|---|
| `SeismicFlow.Api` | Minimal API endpoints, middleware, Dockerfile |
| `SeismicFlow.Application` | CQRS commands/queries (MediatR), interfaces |
| `SeismicFlow.Domain` | Aggregates, entities, domain events |
| `SeismicFlow.Infrastructure` | EF Core, per-tenant PostgreSQL persistence |
| `SeismicFlow.Infrastructure.External` | MQTT consumer, Keycloak integration |
| `SeismicFlow.Shared` | Result type, shared primitives |

### Key design decisions

- **Multi-tenancy via schema isolation** — each tenant gets its own PostgreSQL schema. The master DB holds tenant registry; `TenantMiddleware` routes each request to the correct schema.
- **Auth via Keycloak** — JWT Bearer tokens. Roles: `super-admin`, `tenant-admin`, `device-read`, `device-write`.
- **MQTT auth via API** — Mosquitto delegates authentication and ACL checks to `/mqtt/auth/user` and `/mqtt/auth/acl` on the API (mosquitto-go-auth plugin).
- **SSE streaming** — `ReadingEventBus` is a singleton in-process channel bus. MQTT consumer publishes; SSE endpoint subscribes per `(tenantId, deviceId)`.

---

## API Endpoints

| Method | Path | Policy | Description |
|---|---|---|---|
| `POST` | `/api/v1/tenants` | SuperAdmin | Create tenant + provision DB schema + Keycloak group |
| `GET` | `/api/v1/tenants` | SuperAdmin | List all tenants |
| `GET` | `/api/v1/tenants/{id}` | TenantAdmin | Get tenant by ID |
| `POST` | `/api/v1/devices` | DeviceWrite | Register a device |
| `GET` | `/api/v1/devices` | DeviceRead | List devices for current tenant |
| `GET` | `/api/v1/devices/{id}` | DeviceRead | Get device by ID |
| `PATCH` | `/api/v1/devices/{id}/deactivate` | DeviceWrite | Deactivate device |
| `GET` | `/api/v1/devices/{id}/mqtt-credentials` | DeviceWrite | Get MQTT credentials |
| `GET` | `/api/v1/devices/{id}/readings` | DeviceRead | Query historical readings |
| `GET` | `/api/v1/devices/{id}/readings/latest` | DeviceRead | Get latest reading |
| `GET` | `/api/v1/devices/{id}/readings/stream` | DeviceRead | SSE real-time stream |

Swagger UI is available at `/swagger` in Development mode.

### SSE stream — browser usage

The browser `EventSource` API cannot send `Authorization` headers. Pass the JWT as a query parameter instead:

```js
const es = new EventSource(
  `/api/v1/devices/${deviceId}/readings/stream?access_token=${jwtToken}`
);

es.onmessage = (e) => {
  const reading = JSON.parse(e.data);
  // { deviceId, channel, timestamp, sampleRate, samples }
};
```

### MQTT topic format

Devices publish to:

```
tenant/<tenantId>/devices/<deviceId>/data
```

Payload (JSON):

```json
{
  "channel": "HHZ",
  "timestamp": "2024-01-01T00:00:00Z",
  "sampleRate": 100,
  "samples": [0.1, 0.2, ...]
}
```

---

## Running locally

### Prerequisites

- Docker & Docker Compose
- `.env` file (copy from `.env.example`)

```bash
cp .env.example .env
# Fill in values in .env
```

### Start all services

```bash
docker compose up --build
```

| Service | URL |
|---|---|
| API | http://localhost:5000 |
| Swagger | http://localhost:5000/swagger |
| Keycloak | http://localhost:8080 |
| PostgreSQL | localhost:5432 |
| MQTT | localhost:1883 |

### Health checks

```
GET /health        — overall
GET /health/ready  — postgres + keycloak
GET /health/live   — liveness probe
```

---

## Configuration

All secrets are supplied via environment variables (see `.env.example`). `appsettings.json` contains non-secret defaults only.

| Variable | Description |
|---|---|
| `POSTGRES_PASSWORD` | Master DB password |
| `KEYCLOAK_ADMIN_PASSWORD` | Keycloak admin console password |
| `KEYCLOAK_CLIENT_SECRET` | `seismicflow-admin` client secret |
| `TENANT_DB_PASSWORD_<SLUG>` | Per-tenant DB password |
