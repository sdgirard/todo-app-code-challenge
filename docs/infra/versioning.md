# App Versioning

Semantic versioning driven by CI via GitHub Actions.

## How it works

- **`VERSION`** (repo root) holds just `Major.Minor` (e.g. `0.1`) — hand-bumped by me when a major/minor release is intended, shared by every service. This is the only manual part.
- **`scripts/generate-version.sh <prefix>`** reads `VERSION`, finds the latest existing git tag matching `{prefix}-{Major.Minor}.*`, and auto-increments the patch number. Patch is never hand-edited — it's fully derived from tag history. `<prefix>` is the calling workflow's service name (`gateway` or `web`).
  - No existing tag for that prefix+`Major.Minor` → patch starts at `0`.
  - Existing tag `gateway-0.1.3` found → next gateway version is `0.1.4` (git tag `gateway-0.1.4`).
- Bumping `VERSION` to a new `Major.Minor` (e.g. `0.1` → `0.2`) resets the patch sequence back to `0` for that new line, for every service, since the tag search is scoped to `{prefix}-{Major.Minor}.*`.

### Per-service patch sequences

**`todo-app-gateway` and `todo-app-web` do not share one version number.** Each has its own independent patch sequence, tracked via a prefixed git tag (`gateway-0.1.12`, `web-0.1.0`, ...) even though both read the same major.minor from the one `VERSION` file. The Docker image tag itself stays a plain semver with no prefix (`todo-app-gateway:0.1.12`) — the prefix exists only on the git tag, which is repo-internal bookkeeping, not the image identity, and keeps `APP_VERSION` a clean numeric string safe to feed into `dotnet publish`'s `/p:Version=`.

**Why not one shared sequence:** [`dockerfile-organization.md`](./dockerfile-organization.md) moved build+push into each service's own CI workflow (`backend-ci.yml`, `frontend-ci.yml`), so there's no single job left to compute one number both workflows read from. If backend and frontend both change in the same push, both workflows' `generate-version.sh` calls run independently — a single shared sequence would mean both compute the same next patch number and race to create the same git tag, and the second one to push fails on a tag collision. Per-service prefixes remove the race entirely: each service's sequence only ever advances from its own commits, since the two are independently deployable anyway (different pods, different scaling, already only rebuilding independently).

**Legacy compatibility:** the gateway had 12 releases (`0.1.0`–`0.1.11`, unprefixed) before this per-service scheme existed and before the frontend was containerized. `generate-version.sh gateway` checks for a `gateway-0.1.*` tag first and, finding none yet, falls back to the latest unprefixed `0.1.*` tag and continues from there (next release: `gateway-0.1.12`) — rather than restarting at `gateway-0.1.0`, which would collide with the `todo-app-gateway:0.1.0` image already pushed to Harbor months ago. `web` has no such legacy tags, so it starts fresh at `web-0.1.0`. This fallback is a one-time bridge, not a permanent feature of the script.

## CI wiring (`.github/workflows/backend-ci.yml`, `.github/workflows/frontend-ci.yml`)

No separate orchestrator workflow — each service's own CI file ends with a `build-and-push` job, gated to real pushes to `main` (`github.event_name == 'push' && github.ref == 'refs/heads/main'`, so PRs never publish):

1. **Generate version** — checks out full git history (`fetch-depth: 0`, needed to see all existing tags), runs `sh scripts/generate-version.sh <gateway|web>`, captures `version` and `git_tag` as step outputs. Also records the current UTC timestamp as a `build-date` output — not something either image's Dockerfile can determine on its own without it being passed in.
2. **Build and push** — builds and pushes that one image to Harbor, tagged with `latest`, the commit SHA, and the generated version (e.g. `docker.thecameraeye.ca/todo-app/todo-app-gateway:0.1.12`). Stamps `org.opencontainers.image.version`/`.revision`/`.source` OCI labels.
3. **Create and push git tag** — only runs after a successful build+push (it's a later step in the same job), creates the prefixed annotated git tag (e.g. `gateway-0.1.12`) and pushes it back to the repo, using the workflow's own `GITHUB_TOKEN` (via `permissions: contents: write`) — no separate secret to create.

If the build/push step fails, the job stops before tagging — the tag only exists for versions that actually got published. Since each workflow is scoped by `paths:` to its own service's files, a push that only touches the other service never triggers this pipeline at all.

## Reading the version at runtime: `GET /version`

The generated version, commit SHA, and build date aren't just Harbor tags/OCI labels — they're also baked into the `todo-app-gateway` image itself and readable from the running app, so "what's actually deployed" can be checked by hitting the app directly instead of cross-referencing a tag against a commit.

- **How it gets in:** `backend-ci.yml`'s `build-and-push` job passes `APP_VERSION`, `APP_COMMIT_SHA`, and `APP_BUILD_DATE` as Docker `build-args` (from that same job's `generate-version.sh gateway` output). `docker/Dockerfile.gateway`'s build stage forwards them into `dotnet publish` as `/p:Version=${APP_VERSION}` and `/p:InformationalVersion="${APP_VERSION}+${APP_COMMIT_SHA}.${APP_BUILD_DATE}"`, which the .NET SDK bakes into the compiled assembly's `AssemblyInformationalVersionAttribute` — standard build metadata, not a custom mechanism.
- **How it's read back:** `GET /version` (`backend/src/TodoApi.Gateway/VersionEndpoint.cs`) reads that attribute via reflection at request time and parses the `{version}+{commit}.{buildDate}` format back into its three parts:
  ```json
  { "version": "0.1.12", "commit": "a1b2c3d", "buildDate": "2026-09-19T18:00:00Z" }
  ```
- **Local/no-build-args behavior:** the Dockerfile defaults these args to `0.0.0-dev` / `unknown` / `unknown` if built without `--build-arg` (a plain local `docker build`). Running via `dotnet run` locally is different again — the .NET SDK's SourceLink integration auto-populates `InformationalVersion` with the SDK's default `Version` (`1.0.0`) plus the real local git commit SHA (e.g. `1.0.0+3592e45...`), which `/version` parses the same way; `buildDate` comes back `unknown` in that case since SourceLink doesn't supply one.
- **Why a dedicated endpoint, not just the OpenAPI spec's `info.version`:** the OpenAPI doc/Scalar UI are Development-only (see [`backend/overview.md#interactive-ui-for-manual-testing`](../architecture/backend/overview.md#interactive-ui-for-manual-testing)), so they're not visible in the Production home-lab demo. `/version` is mapped unconditionally in `Endpoints.cs`, so it works the same in every environment — including checking what's actually running in the demo deployment.

## Reading the version at runtime (frontend): `GET /version.json`

Same three build args (from `frontend-ci.yml`'s own `generate-version.sh web` call, entirely independent of the backend's), same idea, different mechanism — the frontend has no server-side process to add a route to, so `docker/Dockerfile.app`'s build stage writes a static `dist/version.json` file instead of baking the values into a compiled attribute:

```json
{ "version": "0.1.0", "commit": "a1b2c3d", "buildDate": "2026-09-19T18:00:00Z" }
```

Served by nginx at a fixed path with `Cache-Control: no-store` (see [`container-image-frontend.md#serving-nginx-config-dockernginxconf`](./container-image-frontend.md#serving-nginx-config-dockernginxconf)) so a browser/proxy can't serve a stale version after a new image ships. Same fallback behavior as the backend if built without `--build-arg`: `0.0.0-dev` / `unknown` / `unknown`.

## Related Docs

- [`container-image.md`](./container-image.md) — the `docker/Dockerfile.gateway` build stage that receives these build args
- [`container-image-frontend.md`](./container-image-frontend.md) — the `docker/Dockerfile.app` build stage and `version.json` generation
- [`harbor-registry-setup.md`](./harbor-registry-setup.md) — where these images get pushed
- [`deployment.md`](./deployment.md) — deployment target
