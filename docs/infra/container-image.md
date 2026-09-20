# Container Image Spec

Design for the backend container image (`docker/Dockerfile.gateway`). Containerization is an optional enhancement (`docs/requirements/requirements.md`), not core scope — this doc exists so the image design is deliberate rather than whatever `docker init` would scaffold.

Image name/registry/tags are already fixed by [`harbor-registry-setup.md`](./harbor-registry-setup.md) and [`versioning.md`](./versioning.md) — `docker.thecameraeye.ca/todo-app/todo-app-gateway`, tagged `latest` + commit SHA + semver. This doc covers what's *inside* the image; where the Dockerfile itself lives and how it's named/organized alongside the (future) frontend Dockerfile is covered in [`dockerfile-organization.md`](./dockerfile-organization.md).

## Scope: backend only

This spec and `docker/Dockerfile.gateway` cover the backend (`TodoApi.Gateway` + `TodoApi.Todos`) only, built and pushed as its own image (`todo-app-gateway`) — see [`dockerfile-organization.md`](./dockerfile-organization.md) for why this is a separate image rather than a combined build, and [`container-image-frontend.md`](./container-image-frontend.md) for the frontend image's own spec (`docker/Dockerfile.app`).

## TLS: Kestrel terminates it directly

**Decided against ingress-terminated TLS.** The app runs HTTPS end-to-end, including the hop between the ingress/reverse proxy and the pod — not just at the public edge. Two reasons:

1. **No plaintext on the wire, anywhere** — including cluster-internal traffic. Ingress termination means the ingress→pod hop is cleartext HTTP; that's out of bounds here even though it never leaves the cluster network.
2. **gRPC-ready.** gRPC rides HTTP/2 and needs a real TLS handshake (ALPN negotiates `h2` end-to-end) to work cleanly through a proxy. Ingress-terminated TLS reintroduces HTTP/2-over-cleartext (`h2c`) proxying concerns that most ingress controllers don't handle well alongside regular HTTP/1.1 traffic. This is a REST API today, but deciding the cert story this way now means adding a gRPC endpoint later doesn't force revisiting TLS.

So: the ingress (once designed, see `deployment.md`'s still-TODO Cluster/Ingress section) does **TLS passthrough**, not termination — encrypted bytes go straight to Kestrel in the pod. Kestrel holds the certificate and does the actual handshake.

### Cert format: PFX/PKCS12

- Single file, cert + private key together, password-protected.
- Matches ASP.NET Core's own dev-cert tooling (`dotnet dev-certs https` exports `.pfx` natively) — same format locally and in the cluster, no format-conversion step to maintain.
- Kestrel config points at the cert via a standard ASP.NET Core configuration key. **The path is fine as a plain env var** (it's not sensitive — just a filesystem location); **the password is a secret and is not passed as a literal env var value**, per [`../standards/aspnet-web-api-guidelines.md#secrets-management`](../standards/aspnet-web-api-guidelines.md#secrets-management). It's supplied the same way as the cert itself — a mounted file, read into config via ASP.NET Core's file-based configuration provider (or `Kestrel__Certificates__Default__Password` sourced from a mounted `dotnet user-secrets`-style file locally, and from the same K8s `Secret` volume as the `.pfx` in the cluster — Kubernetes `Secret` volumes project each key as its own file, so `/certs/todo-app.pfx` and `/certs/password` can come from one mounted `Secret`).

```
ASPNETCORE_Kestrel__Certificates__Default__Path=/certs/todo-app.pfx
# Password is NOT set here as a literal value — it's read from a mounted file
# (e.g. /certs/password, from the same K8s Secret volume), not the process environment.
```

### Cert mounting

- **Container:** the cert is *mounted*, never baked into the image. `/certs` is the conventional mount point (documented, not declared as a Docker `VOLUME` — a cert file is a single small file supplied at runtime, not a persistent-data directory that needs Docker-managed volume semantics).
- **Local dev:** `dotnet dev-certs https -ep ./certs/todo-app.pfx -p <password>` once, then mount `./certs` into the container (`docker run -v $(pwd)/certs:/certs ...` or the equivalent in a local compose file if one gets added later).
- **K8s (home-lab demo):** a `Secret` holding the `.pfx` (populated from the Let's Encrypt cert — see `deployment.md`; exact renewal/conversion mechanism into `.pfx` form is still TODO, part of the still-open Cluster/Ingress section), volume-mounted at `/certs` in the pod spec. Rotation means the mounted `Secret` updates and the pod picks up the new file — exact rotation mechanics (restart vs. hot-reload) TODO alongside the rest of the cluster/ingress design.
- **Port:** Kestrel listens on a single HTTPS port, `8443` inside the container (avoids needing root to bind <1024). `ASPNETCORE_URLS=https://+:8443` set as an image default via `ENV`, overridable per environment.

## Persistence: SQLite path via `/data`

Per [`../architecture/backend/overview.md#where-the-sqlite-file-lives`](../architecture/backend/overview.md#where-the-sqlite-file-lives), the SQLite file must survive pod restarts, so it can't live on the container's own ephemeral filesystem.

- **Mount point convention:** `/data` inside the container — documented, not a Docker `VOLUME` declaration (same reasoning as the cert path: the actual persistence guarantee comes from the K8s PVC mount, not from Docker's anonymous-volume behavior, and declaring `VOLUME` would create an anonymous volume on plain `docker run` that's easy to lose track of).
- **Connection string via config/env**, not hardcoded in `appsettings.json`, so the path can differ per environment without a rebuild:

```
ConnectionStrings__TodoDb=Data Source=/data/todo.db
```

- **Local dev default:** if unset, falls back to a local relative path (e.g. `Data Source=todo.db` in the working directory) via `appsettings.Development.json` — no mount needed to run locally without Docker.
- **K8s (home-lab demo):** a PVC mounted at `/data` in the pod spec (still TODO in `deployment.md`'s Cluster/Ingress section) plus `ConnectionStrings__TodoDb=Data Source=/data/todo.db` set in the pod's env/config.
- **Local Docker run (optional):** `docker run -v todo-data:/data ...` to persist across container restarts; omitting the mount is fine for throwaway local testing (matches the in-memory/file-based minimum the requirements already treat as acceptable for non-demo use).

## Base images: pinned by digest

Both stages pin their base image to a specific digest, not a mutable tag:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:<digest> AS build
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:<digest> AS runtime
```

`10.0` (and even a specific patch tag like `10.0.1`) is a **mutable** tag — Microsoft repoints it whenever a new patch/security update ships. Without pinning to a digest, rebuilding this image months from now (or in a fresh CI cache) silently pulls a different base image than the one actually tested against, which undermines the reproducibility a Dockerfile is supposed to provide in the first place. The tradeoff — remembering to bump the digest for patch/security updates — is deliberate and manual here (no Dependabot/Renovate wired up for this yet); acceptable for this project's scope, revisit if that becomes a real maintenance burden.

### Debian, not Alpine — for now

`mcr.microsoft.com/dotnet/aspnet:10.0` (Debian-based) is the runtime base, not `10.0-alpine`. Alpine is meaningfully smaller — pulled and measured directly: **262MB (Debian) vs. 130MB (Alpine)**, roughly half — a bigger gap than "marginal," so it's a real candidate for later, not dismissed. Staying on Debian for now because:

- **Native SQLite interop is a real risk on musl, not a theoretical one.** This project's persistence layer is EF Core + SQLite (`Microsoft.Data.Sqlite` → `SQLitePCLRaw`), which ships native binaries per-RID; the `linux-musl-x64` path is supported but less battle-tested than glibc, and this project actually exercises that native path (unlike a pure-managed-code service where Alpine would be a free win).
- **The `ENABLE_DEBUG`/`vsdbg` remote-debugging path** (see Remote Debugging below) is more reliably supported on Debian/Ubuntu-based images; `garmin-fit-converter`'s `Dockerfile.zwiftDeviceSwitcher` (the source of that debug pattern) also runs on `mcr.microsoft.com/dotnet/runtime:10.0`, Debian-based, not Alpine — no existing precedent here for vsdbg-on-Alpine to lean on.
- Multi-stage build (no SDK in the shipped image) is already the dominant size win; Alpine would be a second, smaller-than-it-looks-relative-to-that optimization on top.

**Revisit Alpine as a size optimization** once the Dockerfile exists and there's something real to benchmark against (actual published image size, actual CI pull/push times) — at that point, verify `Microsoft.Data.Sqlite` loads correctly under `linux-musl-x64` and confirm (or route around) `vsdbg`'s Alpine support before switching. Not blocking today; tracked as a known future optimization, not an open question about whether it's worth doing.

## Multi-arch: `linux/amd64` + `linux/arm64`

**Decided: build and push both architectures**, as a single multi-platform manifest list under one tag. Driver: local dev happens on Apple Silicon (arm64), while the home-lab demo cluster's nodes are amd64 — a single-arch `amd64`-only image (what `backend-ci.yml` builds today) means anything run locally on the Mac either needs QEMU emulation (slow, and risky specifically because this project has native SQLite interop) or a separate one-off local build that isn't the same image being shipped. Multi-arch removes that gap: the exact manifest pushed to Harbor pulls and runs natively on both.

- **This is local-dev convenience, not a cluster requirement** — the home-lab nodes are amd64-only as far as currently known, so `arm64` isn't load-bearing for the actual demo deployment. It's still built and pushed because the alternative (Mac-only emulated testing against a differently-built image) is worse than the small extra CI cost of building both.
- **CI mechanics:** `backend-ci.yml` gets `docker/setup-qemu-action` (lets the `ubuntu-latest`/amd64 runner cross-build the `arm64` variant) ahead of the existing `docker/setup-buildx-action`, and the `build-push-action` step adds `platforms: linux/amd64,linux/arm64`. `buildx`/`build-push-action` produces one manifest list under the existing tags (`latest`, `${{ github.sha }}`, the semver tag) — Docker/containerd on either architecture pulls the same tag and automatically resolves to the matching platform image, no separate tags per arch needed.
- **No Dockerfile changes required for this specific case.** Unlike `garmin-fit-converter`'s `Dockerfile.zwiftDeviceSwitcher` (which pins a single RID via `--platform=$TARGETPLATFORM` and generates an EF Core migrations bundle — an architecture-specific self-contained executable that has to be built per-target), this project's `dotnet publish` is framework-dependent (see above) and doesn't need an RID-specific build step; the same `dotnet publish` invocation works correctly under either target platform via buildx's automatic per-platform build, without needing `$TARGETPLATFORM`/`$BUILDPLATFORM` handling in the Dockerfile itself.
- **Local multi-arch build/push** (outside CI, e.g. testing before a push): `docker buildx build -f docker/Dockerfile.gateway --platform linux/amd64,linux/arm64 -t docker.thecameraeye.ca/todo-app/todo-app-gateway:local --push .` (a `--push` is required for a true multi-platform build — `--load` only works for a single platform at a time, matching the host).
- **Digest pinning caveat:** the base-image digest pinning above (`FROM ...@sha256:<digest>`) needs the **manifest-list digest** for the base image, not a single-platform image digest — `docker buildx imagetools inspect mcr.microsoft.com/dotnet/aspnet:10.0` shows the manifest-list digest that resolves correctly per-platform; pinning to a single-platform digest by mistake would pin the whole multi-arch build to one architecture's base image.

## Build: multi-stage, cache-optimized

Two stages — no reason for more with a single deployable project graph (Gateway + one feature library):

1. **Build stage** — `mcr.microsoft.com/dotnet/sdk:10.0` (digest-pinned, see above). Restores and publishes `TodoApi.Gateway` (which pulls in `TodoApi.Todos` via project reference). This is also where the build-time OpenAPI spec generation (`OpenApiGenerateDocumentsOnBuild`, see `overview-architecture.md`) runs as a side effect of `dotnet build`/`publish` — no separate step needed in the Dockerfile for that.
2. **Runtime stage** — `mcr.microsoft.com/dotnet/aspnet:10.0` (digest-pinned; runtime-only image, no SDK — smaller, no build tooling in the shipped image). Copies the publish output from stage 1.

### Framework-dependent, not self-contained

`dotnet publish` runs **framework-dependent** (the default), not `--self-contained`. The runtime stage already uses `mcr.microsoft.com/dotnet/aspnet` specifically because it ships the shared ASP.NET Core runtime — a self-contained publish would bundle a second copy of the runtime into the image on top of the one already in the base layer, growing the image for no benefit. Self-contained only earns its keep when the target doesn't already have a matching runtime available (e.g. publishing to a bare `debian`/`alpine` base with no .NET installed at all) — not the case here.

### Layer ordering — cache the expensive things first

Docker/BuildKit caches each layer and invalidates everything *after* the first changed layer, so the build stage is ordered from least-frequently-changed to most-frequently-changed, same pattern used in other backend projects (e.g. `garmin-fit-converter`'s `Dockerfile.zwiftDeviceSwitcher`):

1. **`.csproj`/`Directory.*.props` files only**, copied before any source (`COPY backend/**/*.csproj ...`, `COPY backend/Directory.Build.props ...`) — this layer only invalidates when a package reference or project reference changes, not on every source edit.
2. **`dotnet restore`** against just those project files — cached as long as the csproj layer above is unchanged. This is the expensive step (NuGet resolution); isolating it behind the csproj-only `COPY` means a pure code change never re-triggers it.
3. **Rest of the source (`COPY backend/src/ ...`)** — this is the layer that changes on essentially every build, so it's pushed as late as possible, after the expensive restore is already cached.
4. **`dotnet publish`** — only re-runs the compile, not the restore, when only source changed.

`--no-restore` on the `publish` step (restore already happened in its own cached layer) avoids a redundant implicit restore.

### CI cache reuse

`backend-ci.yml` already uses `docker/build-push-action@v6` with `cache-from: type=gha` / `cache-to: type=gha,mode=max` (see `harbor-registry-setup.md`) — BuildKit's GitHub Actions cache backend, which persists layer cache between CI runs, not just within one build. Combined with the layer ordering above, a CI run that only changed application source skips the restore layer entirely rather than re-downloading NuGet packages every push. No changes needed to that workflow for this — it already does the right thing once the Dockerfile's layers are ordered correctly.

### `.dockerignore`

Excludes `bin/`, `obj/`, `.git/`, and (once it exists) frontend `node_modules`/`dist` — keeps the build context small (faster context upload, especially relevant for the SDK stage) and prevents locally-built artifacts from shadowing what the container build produces itself.

### OCI labels

The Dockerfile itself doesn't declare `org.opencontainers.image.version`/`.revision`/`.source` via its own `LABEL` instructions — those are already stamped at build time by `backend-ci.yml`'s `docker/build-push-action@v6` step (`labels:`, see [`versioning.md`](./versioning.md)), which is the layer that actually has the version/commit/source values available (from the `version` job and `github.sha`/`github.repository`). Duplicating them as static `LABEL` lines in the Dockerfile would either be wrong (baked-in placeholder values) or require build-arg plumbing that already exists one layer up — not worth it for this project.

### Version stamping (`APP_VERSION`/`APP_COMMIT_SHA`/`APP_BUILD_DATE`)

Distinct from the OCI labels above — those describe the image as a registry artifact, these get baked into the .NET assembly itself so the *running application* can report its own version via `GET /version` (see [`versioning.md#reading-the-version-at-runtime-get-version`](./versioning.md#reading-the-version-at-runtime-get-version)). Three build args in the build stage (`ARG APP_VERSION=0.0.0-dev`, `ARG APP_COMMIT_SHA=unknown`, `ARG APP_BUILD_DATE=unknown`), passed to `dotnet publish` as `/p:Version=${APP_VERSION}` and `/p:InformationalVersion="${APP_VERSION}+${APP_COMMIT_SHA}.${APP_BUILD_DATE}"`. `backend-ci.yml` passes the real values via `build-args:`; a plain local `docker build` with no `--build-arg` falls back to the defaults rather than failing.

## Runtime user

Runs as a **non-root user** in the final image — `mcr.microsoft.com/dotnet/aspnet` ships a built-in unprivileged `app` user (UID 64198 on Linux images) for exactly this; the Dockerfile switches to it (`USER app`) rather than running as root, standard container hardening with no extra cost here.

## Remote debugging: `ENABLE_DEBUG` build arg

Same pattern as `garmin-fit-converter`'s `Dockerfile.zwiftDeviceSwitcher` — **one `Dockerfile`, not a separate debug Dockerfile**, with a build arg that conditionally layers on remote-debugging tooling. Keeps the debug and production paths from drifting apart (one file, one set of `COPY`/build steps) while still producing a normal, debug-tool-free image by default.

- **`ARG ENABLE_DEBUG=false`** in the runtime stage. Defaults off — the image pushed to Harbor by `backend-ci.yml` never has debug tooling unless explicitly built with the arg set, since that workflow doesn't pass it.
- **When `true`:** installs [`vsdbg`](https://aka.ms/getvsdbgsh) (the .NET remote debugger VS/VS Code's "Attach to process over SSH/Docker" flow uses) via Microsoft's install script, plus a small set of diagnostic packages (`procps`, `lsof`, `net-tools`) useful when poking at a running container. When `false`, none of that is installed — the conditional lives inside a single `RUN` so the debug-tooling layer doesn't exist at all in a non-debug build (not just "installed then hidden").
- **Debug port:** `EXPOSE 4024` (vsdbg's conventional port in this pattern) — `EXPOSE` is metadata only, doesn't bind anything by itself, so it's harmless to always declare it even in non-debug builds.
- **Entrypoint wrapper:** a small `/app/entrypoint.sh` checks for a marker file dropped only when `ENABLE_DEBUG=true` (e.g. `/tmp/debug_mode`), and if present, forces `ASPNETCORE_ENVIRONMENT=Development`/`DOTNET_ENVIRONMENT=Development` before `exec`'ing the real command — so a debug build also gets Development-mode behavior (Scalar UI, detailed errors) without a separate image variant.
- **Non-root user still applies** in debug builds — `vsdbg` and the app binaries stay owned by the same unprivileged runtime user; debugging doesn't require root.

**The wrapper must end with `exec "$@"`, not a bare/backgrounded call to the real command.** `exec` replaces the shell script's own process rather than spawning a child under it — without it, the wrapper script (PID 1 in the container) is what receives `SIGTERM` on `docker stop`/pod termination, and a plain `dotnet zwiftDeviceSwitcher.dll` invocation (no `exec`) leaves that signal never reaching the actual .NET process. The container would then sit unresponsive until Docker/K8s gives up waiting and sends `SIGKILL` — a hard-killed process instead of Kestrel's normal graceful shutdown (in-flight requests dropped, no clean connection drain). This applies to both the debug and non-debug entrypoint forms, not just the debug wrapper — any shell-script entrypoint in this Dockerfile needs to `exec` its final command.

### Building and running the debug variant

No separate Dockerfile or CI job — just pass the build arg:

```bash
docker build -f docker/Dockerfile.gateway --build-arg ENABLE_DEBUG=true -t todo-app-gateway:debug .
docker run --rm -p 8443:8443 -p 4024:4024 \
  -v $(pwd)/certs:/certs \
  --entrypoint tail todo-app-gateway:debug -f /dev/null   # keep container alive; attach debugger, then attach to the dotnet process
```

A `docker-compose.debug.yml` overlay (mirroring the `garmin-fit-converter` pattern) is a reasonable follow-up once local compose tooling exists for this repo — not required to have the capability, just a convenience wrapper around the same `docker build --build-arg ENABLE_DEBUG=true` + port-mapping + keep-alive-entrypoint combination.

## Health check

Not adding a Docker `HEALTHCHECK` instruction in the image itself — K8s liveness/readiness probes (still TODO in `deployment.md`'s Cluster/Ingress section) are the mechanism that actually matters for the home-lab deployment, and a Docker-level `HEALTHCHECK` would be redundant with those once they exist. Revisit only if the image needs to run somewhere without a K8s-style prober (e.g. plain `docker run` in front of something that reads container health status).

## Verified

`docker/Dockerfile.gateway` implements this spec and has been smoke-tested locally:

- Build succeeds; `openapi.json` generation and rename runs correctly during publish.
- HTTPS via mounted `.pfx` confirmed working end-to-end (HTTP/2 negotiated over real TLS).
- Non-root `app` user confirmed (`id` inside the running container).
- `ENABLE_DEBUG=true` installs `vsdbg` (tested on `linux/arm64`, i.e. natively on Apple Silicon) with no leftover apt cache; `ENABLE_DEBUG=false` (default) has zero debug tooling.
- Entrypoint `exec "$@"` correctness confirmed — PID 1 inside the container is `dotnet`, not the wrapper script; `docker stop` completes in ~0.15s (true graceful shutdown, not a SIGKILL timeout).
- Production image: 267MB. Debug image: 719MB (the ~450MB delta is `vsdbg` itself, not a cleanup bug).

**Not yet verified** (needs a real push, not just a local build): the multi-arch manifest actually publishing correctly via `backend-ci.yml`'s CI run, and the mounted-secret-file cert password path (the smoke test above used a literal env var for the password since `AddKeyPerFile` wiring in `Program.cs` is still an open item — see below).

## Open Questions / TODO

- `docker-compose.debug.yml` convenience wrapper for the `ENABLE_DEBUG` build — not required, just a nice-to-have if local compose tooling gets added.
- Verify the multi-arch push actually works end-to-end once `backend-ci.yml` runs for real (local smoke-testing above only covered a single-platform build).

The three cert/PVC K8s-side items that used to live here (cert password → Kestrel config wiring, Let's Encrypt → mounted `.pfx` conversion/rotation, PVC/StorageClass for `/data`) are tracked in [`outstanding-items.md`](./outstanding-items.md) instead — they're pod-spec/cluster decisions blocked on `deployment.md`'s still-TODO Cluster/Ingress section, not things that block writing the Dockerfile itself. See also that doc for container resource limits and image vulnerability scanning, deferred out of this doc entirely.

## Related Docs

- [`container-image-frontend.md`](./container-image-frontend.md) — frontend image design (`docker/Dockerfile.app`)
- [`deployment.md`](./deployment.md) — TLS strategy (dev cert locally / Let's Encrypt for the demo), deployment target, still-TODO cluster/ingress details
- [`harbor-registry-setup.md`](./harbor-registry-setup.md) — registry/image name, build & push commands
- [`versioning.md`](./versioning.md) — image tagging scheme, existing OCI label stamping in CI
- [`outstanding-items.md`](./outstanding-items.md) — deferred infra items (resource limits, image scanning)
- [`../architecture/backend/overview.md`](../architecture/backend/overview.md#where-the-sqlite-file-lives) — SQLite persistence design, PVC rationale
