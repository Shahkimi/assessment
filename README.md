# TaskFlow — Mini Task Manager

A small full-stack task manager built for the Sophic Automation *SA-IFKM Technical Assessment*
(Software Engineer I, Infineon ADAT).

| Layer      | Technology                                                    |
| ---------- | ------------------------------------------------------------- |
| Frontend   | Angular 18 (standalone components, signals, reactive forms), served by nginx |
| Backend    | ASP.NET Core 8 Web API, EF Core 8, FluentValidation, Swagger  |
| Database   | PostgreSQL 16                                                 |
| Packaging  | Docker (multi-stage), Docker Compose, Kubernetes manifests    |


---

## 1. Run with Docker Compose

Prerequisites: Docker Desktop (or Docker Engine) with the Compose plugin. Nothing else is needed.

```bash
cp .env.example .env        # Windows PowerShell: Copy-Item .env.example .env
# edit .env if you want different passwords / ports
docker compose up --build
```

The first build takes a few minutes (it restores NuGet and npm packages). When the logs show
`Now listening on: http://[::]:8080` the system is ready.

> `.env` is git-ignored. If you skip the copy step, Compose stops immediately with
> `required variable POSTGRES_DB is missing a value: Copy .env.example to .env first`.
> That is deliberate: no secret has a default value anywhere in the repo.

**Log in** at <http://localhost:4200> with:

| Email               | Password   |
| ------------------- | ---------- |
| `admin@taskflow.com` | `Admin@123` |

(These come from `DEMO_USER_EMAIL` / `DEMO_USER_PASSWORD` in `.env`.)

Stop: `docker compose down` (keeps data) or `docker compose down -v` (also deletes the database volume).

### Ports and URLs

| What                     | URL                                   | Notes                                           |
| ------------------------ | ------------------------------------- | ----------------------------------------------- |
| Web app                  | <http://localhost:4200>               | `FRONTEND_PORT`                                  |
| API (direct)             | <http://localhost:5080/api/...>       | `BACKEND_PORT`; the browser normally goes through nginx at `:4200/api` |
| Swagger UI               | <http://localhost:5080/swagger>       | also at <http://localhost:4200/swagger>          |
| Health                   | `/health/live`, `/health/ready`       | liveness (process) / readiness (database)        |
| PostgreSQL               | `localhost:5433`                      | `POSTGRES_PORT`; user/db/password from `.env`    |

Defaults deliberately avoid port 5000 (reserved by Windows on some machines, used by macOS AirPlay)
and 5432 (often already taken by a local PostgreSQL). Change them in `.env` if needed.

### Environment variables (`.env`)

| Variable              | Purpose                                         | Example / default                      |
| --------------------- | ----------------------------------------------- | -------------------------------------- |
| `POSTGRES_DB`         | Database name                                   | `taskflow`                             |
| `POSTGRES_USER`       | Database user                                   | `taskflow`                             |
| `POSTGRES_PASSWORD`   | Database password (avoid `;` and `=`, it is embedded in a connection string) | *(you choose)* |
| `JWT_KEY`             | HMAC signing key, **at least 32 characters**    | `openssl rand -base64 48`              |
| `JWT_ISSUER`          | Token issuer                                    | `taskflow-api`                         |
| `JWT_AUDIENCE`        | Token audience                                  | `taskflow-web`                         |
| `JWT_EXPIRY_MINUTES`  | Token lifetime                                  | `60`                                   |
| `DEMO_USER_EMAIL`     | The single login (assessment allows a hardcoded user) | `admin@taskflow.com`             |
| `DEMO_USER_PASSWORD`  | Password for that login                         | `Admin@123`                            |
| `FRONTEND_PORT` / `BACKEND_PORT` / `POSTGRES_PORT` | Host ports          | `4200` / `5080` / `5433`               |

Inside the backend container Compose turns these into ASP.NET Core configuration keys:
`ConnectionStrings__Default`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__ExpiryMinutes`,
`DemoUser__Email`, `DemoUser__Password`, `Database__ApplyMigrationsOnStartup`.
The API refuses to start if the JWT key is missing or shorter than 32 characters.

---

## 2. Database migrations

EF Core migrations are committed in `backend/src/TaskManager.Api/Data/Migrations/`.

* **Docker Compose / Kubernetes:** nothing to do. `Database__ApplyMigrationsOnStartup=true` makes the API
  apply pending migrations at start-up (the database connection retries while Postgres boots).
* **Plain `dotnet run`:** the flag defaults to `false`, so apply them yourself:

  ```bash
  cd backend
  dotnet tool restore                         # installs the pinned dotnet-ef 8.x
  export ConnectionStrings__Default="Host=localhost;Port=5433;Database=taskflow;Username=taskflow;Password=<your password>"
  dotnet ef database update --project src/TaskManager.Api
  ```

* **Create a new migration:**
  `dotnet ef migrations add <Name> --project src/TaskManager.Api -o Data/Migrations`
* **Generate SQL instead (e.g. for a DBA):**
  `dotnet ef migrations script --idempotent --project src/TaskManager.Api -o migrate.sql`

---

## 3. API summary

All task routes need `Authorization: Bearer <token>`. Errors always use one JSON shape
([RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) problem details):

```json
{
  "type": "https://httpstatuses.io/400",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/tasks",
  "errors": { "title": ["Title is required."] },
  "traceId": "0HNP1UCEPE6ON:00000001"
}
```

| Method & path                     | Auth | Body                                                                 | Success      | Errors              |
| --------------------------------- | ---- | -------------------------------------------------------------------- | ------------ | ------------------- |
| `POST /api/auth/login`            | no   | `{ "email", "password" }`                                            | `200 { token, expiresAtUtc }` | 400 validation, 401 bad credentials |
| `GET /api/tasks`                  | yes  | –                                                                    | `200 [task]` (open first, then due date, then newest) | 401 |
| `POST /api/tasks`                 | yes  | `{ "title", "description?", "priority", "dueDate?" }`                | `201 task`   | 400, 401            |
| `PUT /api/tasks/{id}`             | yes  | same as create                                                       | `200 task`   | 400, 401, 404       |
| `PATCH /api/tasks/{id}/status`    | yes  | `{ "status": "Todo" \| "Done" }`                                     | `200 task`   | 400, 401, 404       |
| `DELETE /api/tasks/{id}`          | yes  | –                                                                    | `204`        | 401, 404            |
| `GET /health/live`, `/health/ready` | no | –                                                                    | `200 Healthy`| 503 when DB is down |

Task fields: `title` (required, ≤ 200 chars), `description` (optional, ≤ 2000), `priority` (`Low` | `Medium` | `High`),
`dueDate` (optional, `yyyy-MM-dd`), `status` (`Todo` | `Done`; new tasks start as `Todo`).

Quick try with curl:

```bash
TOKEN=$(curl -s -X POST http://localhost:4200/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@taskflow.com","password":"Admin@123"}' | python -c "import sys,json;print(json.load(sys.stdin)['token'])")

curl -s -X POST http://localhost:4200/api/tasks -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' -d '{"title":"Hello","priority":"High","dueDate":"2026-12-31"}'
```

---

## 4. Run without Docker (development)

Requirements: .NET 8 SDK, Node 20+ (Angular 18), and a PostgreSQL you can reach.

```bash
# 1) database only
docker compose up -d db                      # exposed on localhost:5433

# 2) backend (terminal 1)
cd backend/src/TaskManager.Api
export ConnectionStrings__Default="Host=localhost;Port=5433;Database=taskflow;Username=taskflow;Password=<POSTGRES_PASSWORD>"
export Jwt__Key="<at least 32 characters>" Jwt__Issuer=taskflow-api Jwt__Audience=taskflow-web
export DemoUser__Email=admin@taskflow.com DemoUser__Password='Admin@123'
export Database__ApplyMigrationsOnStartup=true
export ASPNETCORE_URLS=http://localhost:5080
dotnet run

# 3) frontend (terminal 2) — proxies /api to http://localhost:5080 (see frontend/proxy.conf.json)
cd frontend
npm ci
npm start                                    # http://localhost:4200
```

PowerShell: use `$env:ConnectionStrings__Default = "..."` instead of `export`.

### Tests

```bash
cd backend && dotnet test        # 35 unit tests: validators, TaskService, login/JWT
cd frontend && npm run build     # type-checks and builds the production bundle
```

---

## 5. Kubernetes

Manifests are in [`k8s/`](k8s/): namespace, ConfigMap, Secret, Postgres (PVC + Deployment + Service),
backend (Deployment + Service + liveness/readiness/startup probes), frontend (Deployment + Service)
and an Ingress (`/api`, `/swagger` → backend, `/` → frontend). `kustomization.yaml` ties them together.

The cluster must be able to find the images. Build them and, for a local cluster, load them:

```bash
docker build -t taskflow-backend:1.0.0  ./backend
docker build -t taskflow-frontend:1.0.0 ./frontend

# kind:      kind load docker-image taskflow-backend:1.0.0 taskflow-frontend:1.0.0
# minikube:  minikube image load taskflow-backend:1.0.0 && minikube image load taskflow-frontend:1.0.0
# Docker Desktop's built-in Kubernetes uses the local Docker images directly.
# Real cluster: push to your registry and change the image names in backend.yaml / frontend.yaml.

kubectl apply -k k8s/
kubectl -n taskflow get pods -w              # wait until all are Running / Ready
kubectl -n taskflow port-forward svc/frontend 8080:80    # then open http://localhost:8080
```

With an ingress controller installed (e.g. ingress-nginx) the app is also served on the ingress address.

`k8s/secret.yaml` holds **dev placeholders** so the manifests work out of the box. Replace them before
using a real cluster (the file header shows the `kubectl create secret` command). Remove everything again with
`kubectl delete -k k8s/`.

---

## 6. Troubleshooting

| Symptom | Cause and fix |
| ------- | ------------- |
| `required variable ... is missing a value: Copy .env.example to .env first` | `.env` does not exist. Run `cp .env.example .env`. |
| `ports are not available ... bind: An attempt was made to access a socket in a way forbidden` or `port is already allocated` | Another program owns the host port (Windows reserves some ranges, macOS uses 5000). Change `FRONTEND_PORT`, `BACKEND_PORT` or `POSTGRES_PORT` in `.env`, then `docker compose up -d`. |
| Backend exits immediately with `OptionsValidationException ... Key` | `JWT_KEY` is empty or shorter than 32 characters. Fix `.env`. |
| Login says "Invalid email or password." | Credentials must match `DEMO_USER_EMAIL` / `DEMO_USER_PASSWORD` in `.env` (email is case-insensitive, password is case-sensitive). |
| Everything was fine, now every call returns 401 and the app returns to the login page | The JWT expired (`JWT_EXPIRY_MINUTES`) or `JWT_KEY` changed. Sign in again. |
| Changed `POSTGRES_PASSWORD` in `.env` but the backend cannot log in to the database | Postgres only reads the password when it first creates the volume. Run `docker compose down -v` (deletes data) and start again. |
| `GET /health/ready` returns 503 | The API cannot reach PostgreSQL. `docker compose logs db backend`. |
| Browser shows a blank page after a frontend change | Rebuild: `docker compose up --build frontend`. |
| `npm start` shows `ECONNREFUSED` for `/api` | The backend is not running on the port in `frontend/proxy.conf.json` (5080). |
| Pods `ImagePullBackOff` | The cluster cannot see the local images. Load them (see section 5) or push to a registry. |

Useful commands: `docker compose logs -f backend`, `docker compose ps`,
`docker compose exec db psql -U taskflow -d taskflow -c "select * from tasks;"`.

---

## 7. Repository layout

```
backend/    ASP.NET Core API (Controllers → Services → Repositories), tests, Dockerfile
frontend/   Angular app, nginx config, Dockerfile
k8s/        Kubernetes manifests (kustomize)
docker-compose.yml   .env.example
```
