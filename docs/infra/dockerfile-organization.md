# Dockerfile Organization

How multiple Dockerfiles are laid out and named now that there's more than one deployable image — the backend (`TodoApi.Gateway`) and the frontend SPA. This doc covers *where files live and what images are called*; [`container-image.md`](./container-image.md) (backend) and [`container-image-frontend.md`](./container-image-frontend.md) (frontend) cover what's actually inside each image (TLS, build stages, etc.), which is unaffected by this reorganization.

## Layout: `docker/` at the repo root

```text
todo-app-code-challenge/
├── docker/
│   ├── Dockerfile.gateway      # backend (TodoApi.Gateway) — see container-image.md
│   ├── Dockerfile.app          # frontend SPA — see container-image-frontend.md
│   └── nginx.conf              # frontend's nginx config, copied into Dockerfile.app's runtime stage
├── .dockerignore
├── backend/
├── frontend/
└── ...
```

**Decided against** a `Dockerfile` inside each service directory (`backend/Dockerfile`, `frontend/Dockerfile`). A shared `docker/` directory, one file per service, matches the pattern already used in other projects (e.g. `garmin-fit-converter`'s `docker/Dockerfile.zwiftDeviceSwitcher`) — container-build concerns stay grouped in one place, separate from application source, and both Dockerfiles are visible together rather than requiring a hunt through `backend/` and `frontend/` to find them. The tradeoff (`COPY` paths inside each Dockerfile are relative to the repo-root build context, not the service directory) is minor and already how `docker/Dockerfile.gateway` is written (`COPY backend/src/ backend/src/`).

**Build context is always the repo root**, not `docker/` — each Dockerfile is invoked with `-f docker/Dockerfile.<name>` but a plain `.` context, same as today:

```bash
docker build -f docker/Dockerfile.gateway -t todo-app-gateway .
```

A single `.dockerignore` at the repo root covers both Dockerfiles' build contexts (see [`container-image.md#dockerignore`](./container-image.md#dockerignore) for what it excludes) — no per-Dockerfile `.dockerignore` needed since they share one context.

## Naming: `Dockerfile.<service>`, not `Dockerfile.<image-name>`

- `Dockerfile.gateway` — matches the `TodoApi.Gateway` project name, not the Harbor image name (`todo-app-gateway`). Keeps the filename stable if the image name ever changes, and ties it to the actual project it builds rather than a registry artifact name.
- `Dockerfile.app` — the frontend SPA's Dockerfile. Named `.app`, not `.web` or `.frontend`, kept as a deliberate final name (not the placeholder it started as) to leave room if "app" later needs disambiguating from a future non-web client. See [`container-image-frontend.md`](./container-image-frontend.md) for what's inside it.

## Image naming: `todo-app-<service>`

Two distinct images under the same `todo-app` Harbor project (see [`harbor-registry-setup.md`](./harbor-registry-setup.md)), not one shared `todo-app` image with a tag suffix:

| Image | Dockerfile | Status |
|---|---|---|
| `docker.thecameraeye.ca/todo-app/todo-app-gateway` | `docker/Dockerfile.gateway` | Exists, built and pushed by CI |
| `docker.thecameraeye.ca/todo-app/todo-app-web` | `docker/Dockerfile.app` | Exists, built and pushed by CI |

**Why separate images, not one `todo-app` image with `-backend`/`-frontend` tags:** these are two genuinely different deployables (different runtime, different pod in K8s, independently scalable/updatable) — Docker tags are for versions of the *same* artifact, not for naming unrelated artifacts. Each gets its own repository within the `todo-app` project, each with its own `latest`/SHA/semver tags (see [`versioning.md`](./versioning.md)).

This is a rename from the single `todo-app` image name used before this reorganization — no images had actually been pushed under the old name yet (containerization is still in initial setup, not a released feature), so there's no migration/deprecation concern.

## CI: one workflow per service, path-filtered

Each service's CI workflow (`.github/workflows/backend-ci.yml`, `.github/workflows/frontend-ci.yml`) owns its own full pipeline — test, then build+push its own image — rather than a separate shared `docker-publish.yml` orchestrating both:

```yaml
# backend-ci.yml
on:
  push:
    branches: [main]
    paths: ["backend/**", "docker/Dockerfile.gateway"]
  pull_request:
    branches: [main]
    paths: ["backend/**", "docker/Dockerfile.gateway"]

jobs:
  test: ...
  build-and-push:
    needs: test
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    ...
```

`frontend-ci.yml` mirrors this exactly, scoped to `frontend/**` (plus its own Dockerfile/nginx config) instead.

**This replaced an earlier single shared `docker-publish.yml`** that ran both images as matrix entries out of one workflow, gated by a separate path-detection job. That design shared one version number across both images per release and had a real bug: `build-and-push`'s implicit `needs`-based `if` condition didn't survive a partially-skipped dependency chain, so it silently skipped the frontend image on a real frontend-only push (caught in production CI, not locally). Moving build+push into each service's own CI file removes that cross-workflow coordination entirely — a service's pipeline is self-contained, so there's no shared-state bug surface between the two.

**Trigger scope: `paths:` filters, not a separate `changes` job.** GitHub Actions' own `paths:` filter on `push`/`pull_request` does the job a `dorny/paths-filter` step used to do — a frontend-only push simply never triggers `backend-ci.yml` at all, rather than triggering it and then skipping its jobs internally.

**Build+push is push-to-`main`-only, not PRs.** Both workflows' `test` job runs on PRs too (for review feedback), but `build-and-push` guards on `github.event_name == 'push' && github.ref == 'refs/heads/main'` — a PR should be verified, never published.

**Versioning is now per-service, not shared.** See [`versioning.md#per-service-patch-sequences`](./versioning.md#per-service-patch-sequences) for why: two independent build-and-push jobs racing to generate/tag "the next shared version" in the same push (when both services change together) would have the second one fail on a git tag collision. Each service's `scripts/generate-version.sh <prefix>` call tracks its own patch sequence (`gateway-0.1.12`, `web-0.1.0`, ...) against a shared `VERSION` file's major.minor — so `todo-app-gateway` and `todo-app-web` can be on different patch numbers at any given time, which is fine since they're independently deployable and already only rebuild independently.

**GHA cache scoping:** each workflow's `cache-from`/`cache-to` includes `scope: ${{ env.IMAGE }}` — without this, both images would share one BuildKit GitHub Actions cache key and evict each other's layers on every run (backend and frontend have completely different dependency graphs, so a shared cache key would provide no benefit and just thrash).

## Related Docs

- [`container-image.md`](./container-image.md) — backend image design (what's inside `docker/Dockerfile.gateway`)
- [`container-image-frontend.md`](./container-image-frontend.md) — frontend image design (what's inside `docker/Dockerfile.app`)
- [`harbor-registry-setup.md`](./harbor-registry-setup.md) — registry/project setup, image naming, scanning
- [`versioning.md`](./versioning.md) — per-service version numbering scheme
- [`deployment.md`](./deployment.md) — where both images get deployed
