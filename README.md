# TaskFlow — Mini Task Manager

A small full-stack task manager built for the Sophic Automation *SA-IFKM Technical Assessment*
(Software Engineer I, Infineon ADAT).

| Layer      | Technology                                                    |
| ---------- | ------------------------------------------------------------- |
| Frontend   | Angular 18 (standalone components, signals, reactive forms), served by nginx |
| Backend    | ASP.NET Core 8 Web API, EF Core 8, FluentValidation, Swagger  |
| Database   | PostgreSQL 16                                                 |
| Packaging  | Docker (multi-stage), Docker Compose, Kubernetes manifests    |
| Delivery   | GitHub Actions CI/CD, HPA autoscaling, blue/green releases (section 5 and 8) |


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

* **Docker Compose:** nothing to do. `Database__ApplyMigrationsOnStartup=true` makes the API
  apply pending migrations at start-up (the database connection retries while Postgres boots).
* **Kubernetes:** a one-off Job runs `dotnet TaskManager.Api.dll --migrate-only` before each release
  (see section 5); the API pods themselves do not migrate, so any number of replicas is safe.
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

## 5. Kubernetes (HPA + blue/green)

```
k8s/                  infrastructure: namespace, ConfigMap, Secret, Postgres, Ingress, live Service
k8s/app/{backend,frontend}/   one colour's Deployment + HPA + PodDisruptionBudget + Service (templates)
k8s/slots/{blue,green}/       the templates suffixed -blue / -green and labelled slot=blue|green
k8s/jobs/migrate.yaml         database migration Job (run by the script before each release)
scripts/bluegreen.sh          bootstrap | deploy | switch | rollback | status
```

**How it works.** Two complete copies ("colours") of backend and frontend can run side by side.
The Ingress sends all traffic to the `frontend` Service, whose selector names the live colour
(`slot: blue` or `green`); the frontend's nginx proxies `/api`, `/swagger` and `/health` to the *same* colour's
backend (`backend-<colour>`). A release deploys the new version to the idle colour, tests it through its own
Services (`frontend-<colour>`), then flips the one selector, so web and API change together and rolling back is
flipping it again. Both colours share the database, ConfigMap and Secret, so a login token issued by one colour
is accepted by the other.

**Autoscaling.** Each colour has a HorizontalPodAutoscaler per tier (CPU 70 % of the request): backend 2-5 pods,
frontend 2-4 pods, scale-down after 5 minutes of low load, plus a PodDisruptionBudget (`minAvailable: 1`) and
a short `preStop` so pods drain on scale-down. The cluster needs **metrics-server** (kind has none: apply its manifest and add
`--kubelet-insecure-tls`; minikube: `minikube addons enable metrics-server`).
The idle colour stays at its minimum size so rollback is instant, so the baseline is twice the pods of a
single-colour setup.

**Images.** The cluster must be able to find them. For a local cluster:

```bash
docker build -t taskflow-backend:1.0.0  ./backend
docker build -t taskflow-frontend:1.0.0 ./frontend

# kind:      kind load docker-image taskflow-backend:1.0.0 taskflow-frontend:1.0.0
# minikube:  minikube image load taskflow-backend:1.0.0 && minikube image load taskflow-frontend:1.0.0
# Docker Desktop's built-in Kubernetes uses the local Docker images directly.
# Real cluster: push to a registry and set BACKEND_REPO / FRONTEND_REPO (below).
```

**First install, then every release.** The script needs `bash` and `kubectl` (Git Bash or WSL on Windows) and uses your
current `kubectl` context; run it as `bash scripts/bluegreen.sh ...` if the executable bit is not set.

```bash
scripts/bluegreen.sh bootstrap 1.0.0          # infrastructure + migrations + blue colour (live)
kubectl -n taskflow port-forward svc/frontend 8080:80     # then open http://localhost:8080

# new version
docker build -t taskflow-backend:1.0.1  ./backend
docker build -t taskflow-frontend:1.0.1 ./frontend        # (kind load ... as above)
scripts/bluegreen.sh deploy 1.0.1             # migrate, deploy to the idle colour, smoke-test it
scripts/bluegreen.sh switch                   # go live (or: deploy 1.0.1 --switch)
scripts/bluegreen.sh rollback                 # something wrong? back to the previous colour
scripts/bluegreen.sh status

# registry images, e.g. GHCR (published by cd.yml as :latest and :sha-<commit>)
BACKEND_REPO=ghcr.io/<owner>/taskflow-backend FRONTEND_REPO=ghcr.io/<owner>/taskflow-frontend   scripts/bluegreen.sh deploy sha-<commit>
```

`deploy` runs the migration Job first and stops if it fails, waits until each tier has at least its HPA minimum
of ready pods, and smoke-tests the new colour inside the cluster (health, 401 without a token, login, 200 with a
token, and a token from the live colour accepted by the new one). Traffic only moves on `switch`.

Things to know:

* **Migrations must be backward compatible** (add columns/tables first, remove them in a later release): the old
  colour keeps serving while the new schema is applied and until you switch.
* **Do not run `kubectl apply -k k8s/` again** after bootstrap. It would reset the live Service to blue. The script
  only applies the idle colour. `bootstrap` refuses to run twice.
* An open browser tab may fail to load a lazy chunk of the other colour right after a switch; a reload fixes it.
* **Upgrading an existing cluster** from the single-colour manifests: `kubectl -n taskflow delete deploy backend frontend`
  once before `bootstrap` (the old Services are replaced).
* With an ingress controller (e.g. ingress-nginx) the app is also served on the ingress address.
* `k8s/secret.yaml` holds **dev placeholders** so the manifests work out of the box. Replace them before using a real
  cluster (the file header shows the `kubectl create secret` command). Remove everything with `kubectl delete ns taskflow`.

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
| `502 Bad Gateway` from the frontend in Kubernetes, nginx log `backend-blue could not be resolved` | `BACKEND_URL` must be the fully qualified name (`backend-<colour>.taskflow.svc.cluster.local`, set in `k8s/slots/<colour>/kustomization.yaml`); adjust it if your cluster domain is not `cluster.local`. |
| `kubectl get hpa` shows `<unknown>` and pods stay at the minimum | metrics-server is missing or not ready (`kubectl top pods`). |
| Pods `ImagePullBackOff` | The cluster cannot see the local images. Load them (see section 5) or push to a registry. |

Useful commands: `docker compose logs -f backend`, `docker compose ps`,
`docker compose exec db psql -U taskflow -d taskflow -c "select * from tasks;"`.

---

## 7. Repository layout

```
backend/    ASP.NET Core API (Controllers → Services → Repositories), tests, Dockerfile
frontend/   Angular app, nginx config, Dockerfile
k8s/        Kubernetes manifests (kustomize, blue/green + HPA)
scripts/    bluegreen.sh: Kubernetes release helper
.github/    CI/CD workflows (GitHub Actions)
docker-compose.yml   .env.example
```

---

## 8. CI/CD (GitHub Actions)

| Workflow | Runs on | What it does |
|----------|---------|--------------|
| [`ci.yml`](.github/workflows/ci.yml) | every pull request (and reused by `cd.yml`) | backend build + 35 unit tests + "model changed without a migration" check; Angular production build; kubeconform on every kustomization plus checks of the blue/green wiring; both Docker images build; Docker Compose smoke test (stack starts, `/health/ready`, login, protected endpoint returns 401 without and 200 with a token); **kind end-to-end test**: metrics-server, `bootstrap`, `deploy` (traffic stays on blue), `switch`, `rollback`, HPAs active and a backend scale-up under load |
| [`cd.yml`](.github/workflows/cd.yml) | every push to `main` | runs the whole CI first; only if it is green, pushes `ghcr.io/<owner>/taskflow-backend` and `taskflow-frontend` tagged `latest` and `sha-<commit>` |

To make the checks a gate for `main`, set a branch protection rule (Settings → Branches) that requires the CI jobs to pass before merging.
Deploying the published images to a cluster is not automated in CD (no cluster credentials): run `scripts/bluegreen.sh deploy sha-<commit>` with `BACKEND_REPO` / `FRONTEND_REPO` pointing at GHCR (section 5).

---

## 9. Assessment checklist: where each requirement lives

| Requirement | Where |
| ----------- | ----- |
| `docker compose up --build` starts a working system | `docker-compose.yml`, section 1 |
| `.env.example`, no secrets in code or compose | `.env.example`; Compose uses `${VAR:?...}`; Kubernetes uses a Secret with dev placeholders |
| JWT login `POST /api/auth/login` (hardcoded user allowed) | `Controllers/AuthController.cs`; user read from `DemoUser__*` config |
| Task CRUD + `PATCH .../status`, JWT on all of them | `Controllers/TasksController.cs`, section 3 |
| Controller → Service → Repository, EF Core + PostgreSQL | `backend/src/TaskManager.Api/{Controllers,Services,Repositories,Data}` |
| Migrations in the repository | `backend/src/TaskManager.Api/Data/Migrations/`, section 2 |
| Validation, global exception middleware (one JSON error shape), Swagger | `Validation/`, `Middleware/ExceptionHandlingMiddleware.cs`, `/swagger` |
| Angular 18: login (reactive form), guard, interceptor, `/tasks` page, create/edit form, done/undo, delete, loading + friendly errors | `frontend/src/app/{core,features}` |
| `backend/Dockerfile`, `frontend/Dockerfile` (Angular build served by nginx) | multi-stage, both folders |
| Kubernetes: backend, frontend, database Deployment + Service; ConfigMap; Secret | `k8s/app/backend`, `k8s/app/frontend` (instantiated as blue and green by `k8s/slots/*`), `k8s/postgres.yaml`, `k8s/configmap.yaml`, `k8s/secret.yaml` |
| Bonus: Ingress | `k8s/ingress.yaml`: every path goes to the live `frontend` Service, whose nginx proxies `/api` to that colour's backend (one switch moves web and API together) |
| Bonus: backend liveness/readiness probes | `/health/live`, `/health/ready` in `k8s/app/backend/deployment.yaml` |
| README: run, ports/URLs, environment variables, migrations, endpoint summary | sections 1 to 3 |
