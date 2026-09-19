# Dockerfile Organization

How multiple Dockerfiles are laid out and named now that there's more than one deployable image — the backend (`TodoApi.Gateway`) and, once scaffolded, the frontend SPA. This doc covers *where files live and what images are called*; [`container-image.md`](./container-image.md) covers what's actually inside the backend image (TLS, persistence, build stages, debug tooling), which is unaffected by this reorganization.

## Layout: `docker/` at the repo root

```text
todo-app-code-challenge/
├── docker/
│   ├── Dockerfile.gateway      # backend (TodoApi.Gateway) — see container-image.md
│   └── Dockerfile.app          # frontend SPA — not yet written, see below
├── .dockerignore
├── backend/
├── frontend/                   # not yet scaffolded
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
- `Dockerfile.app` (planned) — the frontend SPA's Dockerfile, once `frontend/` is scaffolded. Named `.app`, not `.web` or `.frontend`, to leave room if "app" later needs disambiguating from a future non-web client — but this is a placeholder name, not a firm decision; revisit when the frontend's own architecture doc exists and names things concretely.

## Image naming: `todo-app-<service>`

Two distinct images under the same `inhouse` Harbor project (see [`harbor-registry-setup.md`](./harbor-registry-setup.md)), not one shared `todo-app` image with a tag suffix:

| Image | Dockerfile | Status |
|---|---|---|
| `docker.thecameraeye.ca/inhouse/todo-app-gateway` | `docker/Dockerfile.gateway` | Exists, built and pushed by CI |
| `docker.thecameraeye.ca/inhouse/todo-app-web` | `docker/Dockerfile.app` | Not yet built — `frontend/` doesn't exist yet |

**Why separate images, not one `todo-app` image with `-backend`/`-frontend` tags:** these are two genuinely different deployables (different runtime, different pod in K8s, independently scalable/updatable) — Docker tags are for versions of the *same* artifact, not for naming unrelated artifacts. Each gets its own repository within the `inhouse` project, each with its own `latest`/SHA/semver tags (see [`versioning.md`](./versioning.md)).

This is a rename from the single `todo-app` image name used before this reorganization — no images had actually been pushed under the old name yet (containerization is still in initial setup, not a released feature), so there's no migration/deprecation concern.

## CI: one workflow, matrix build

`docker-publish.yml` stays a single workflow (not split into `docker-publish-gateway.yml` / `docker-publish-web.yml`) — the `build-and-push` job runs as a **matrix** over each image:

```yaml
strategy:
  matrix:
    include:
      - image: todo-app-gateway
        dockerfile: docker/Dockerfile.gateway
      # - image: todo-app-web
      #   dockerfile: docker/Dockerfile.app
```

**Why one workflow:** both images should share the same version number per release (from the single `version` job, via `scripts/generate-version.sh` — see [`versioning.md`](./versioning.md)) rather than each independently generating its own version and drifting out of sync. A matrix keeps that one shared `version` job while still building/pushing each image as its own isolated step. The `web` entry is commented out, not deleted, so re-adding it once `frontend/` and `docker/Dockerfile.app` exist is a one-line uncomment, not a rewrite.

**GHA cache scoping:** each matrix entry's `cache-from`/`cache-to` includes `scope=${{ matrix.image }}` — without this, both images would share one BuildKit GitHub Actions cache key and evict each other's layers on every run (backend and frontend have completely different dependency graphs, so a shared cache key would provide no benefit and just thrash).

**Trigger scope not yet split.** `docker-publish.yml` still triggers on every push to `main`, rebuilding both images regardless of whether backend or frontend actually changed. Path-based trigger filtering (`paths:` per job, or splitting into separate workflows keyed off `backend/**` vs `frontend/**`) is a reasonable follow-up once both images are real and build time/cost becomes a real consideration — not needed while only one image exists.

## Related Docs

- [`container-image.md`](./container-image.md) — backend image design (what's inside `docker/Dockerfile.gateway`)
- [`harbor-registry-setup.md`](./harbor-registry-setup.md) — registry/project setup, image naming, scanning
- [`versioning.md`](./versioning.md) — shared version numbering across both images
- [`deployment.md`](./deployment.md) — where both images get deployed
