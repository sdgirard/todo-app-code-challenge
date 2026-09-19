# App Versioning

Semantic versioning driven by CI via GitHub Actions.

## How it works

- **`VERSION`** (repo root) holds just `Major.Minor` (e.g. `0.1`) — hand-bumped by me when a major/minor release is intended. This is the only manual part.
- **`scripts/generate-version.sh`** reads `VERSION`, finds the latest existing git tag matching `{Major.Minor}.*`, and auto-increments the patch number. Patch is never hand-edited — it's fully derived from tag history.
  - No existing tag for that `Major.Minor` → patch starts at `0`.
  - Existing tag `0.1.3` found → next version is `0.1.4`.
- Bumping `VERSION` to a new `Major.Minor` (e.g. `0.1` → `0.2`) resets the patch sequence back to `0` for that new line, since the tag search is scoped to `{Major.Minor}.*`.

## CI wiring (`.github/workflows/docker-publish.yml`)

Three jobs, in order, on every push to `main`:

1. **`version`** — checks out full git history (`fetch-depth: 0`, needed to see all existing tags), runs `generate-version.sh`, exposes the computed version as a job output. Also records the current UTC timestamp as a `build-date` output (`date -u +%Y-%m-%dT%H:%M:%SZ`), used for the same reason as the version — not something either image's Dockerfile can determine on its own without it being passed in.
2. **`build-and-push`** — a matrix job, one entry per image (currently just `todo-app-gateway`; `todo-app-web` once `frontend/` exists — see [`dockerfile-organization.md`](./dockerfile-organization.md)). Builds and pushes each image to Harbor, tagged with `latest`, the commit SHA, *and* the same generated semantic version shared across every image in the matrix (`docker.thecameraeye.ca/inhouse/todo-app-gateway:0.1.4`). Also stamps `org.opencontainers.image.version`/`.revision`/`.source` OCI labels on each image.
3. **`version-tag`** — only runs after a successful build+push, creates an annotated git tag (`0.1.4`) and pushes it back to the repo, using the workflow's own `GITHUB_TOKEN` (via `permissions: contents: write`) — no separate secret to create.

If the build/push fails, no tag is created — the tag only exists for versions that actually got published.

## Reading the version at runtime: `GET /version`

The generated version, commit SHA, and build date aren't just Harbor tags/OCI labels — they're also baked into the `todo-app-gateway` image itself and readable from the running app, so "what's actually deployed" can be checked by hitting the app directly instead of cross-referencing a tag against a commit.

- **How it gets in:** the `build-and-push` matrix entry for `todo-app-gateway` passes `APP_VERSION`, `APP_COMMIT_SHA`, and `APP_BUILD_DATE` as Docker `build-args` (from the same `version` job outputs used for image tags/labels above). `docker/Dockerfile.gateway`'s build stage forwards them into `dotnet publish` as `/p:Version=${APP_VERSION}` and `/p:InformationalVersion="${APP_VERSION}+${APP_COMMIT_SHA}.${APP_BUILD_DATE}"`, which the .NET SDK bakes into the compiled assembly's `AssemblyInformationalVersionAttribute` — standard build metadata, not a custom mechanism.
- **How it's read back:** `GET /version` (`backend/src/TodoApi.Gateway/VersionEndpoint.cs`) reads that attribute via reflection at request time and parses the `{version}+{commit}.{buildDate}` format back into its three parts:
  ```json
  { "version": "0.1.4", "commit": "a1b2c3d", "buildDate": "2026-09-19T18:00:00Z" }
  ```
- **Local/no-build-args behavior:** the Dockerfile defaults these args to `0.0.0-dev` / `unknown` / `unknown` if built without `--build-arg` (a plain local `docker build`). Running via `dotnet run` locally is different again — the .NET SDK's SourceLink integration auto-populates `InformationalVersion` with the SDK's default `Version` (`1.0.0`) plus the real local git commit SHA (e.g. `1.0.0+3592e45...`), which `/version` parses the same way; `buildDate` comes back `unknown` in that case since SourceLink doesn't supply one.
- **Why a dedicated endpoint, not just the OpenAPI spec's `info.version`:** the OpenAPI doc/Scalar UI are Development-only (see [`backend/overview.md#interactive-ui-for-manual-testing`](../architecture/backend/overview.md#interactive-ui-for-manual-testing)), so they're not visible in the Production home-lab demo. `/version` is mapped unconditionally in `Endpoints.cs`, so it works the same in every environment — including checking what's actually running in the demo deployment.

## Related Docs

- [`container-image.md`](./container-image.md) — the `docker/Dockerfile.gateway` build stage that receives these build args
- [`harbor-registry-setup.md`](./harbor-registry-setup.md) — where these images get pushed
- [`deployment.md`](./deployment.md) — deployment target
