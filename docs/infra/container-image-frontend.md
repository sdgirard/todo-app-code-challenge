# Container Image Spec — Frontend

Design for the frontend container image (`docker/Dockerfile.app`). Companion to [`container-image.md`](./container-image.md), which covers the backend image — read that first for the shared conventions (digest pinning, non-root, multi-arch) this doc doesn't repeat in full.

Image name/registry/tags are fixed by [`harbor-registry-setup.md`](./harbor-registry-setup.md) and [`versioning.md`](./versioning.md) — `docker.thecameraeye.ca/todo-app/todo-app-web`, tagged `latest` + commit SHA + semver, same scheme as the backend. This doc covers what's *inside* the image; where the Dockerfile lives and how it's named/organized alongside the backend's is covered in [`dockerfile-organization.md`](./dockerfile-organization.md).

## Build: two stages, Node then nginx

Same shape as the backend's build/runtime split, different toolchain:

1. **Build stage** — `node:22-alpine` (digest-pinned). `npm ci` against just `package.json`/`package-lock.json` first (its own cached layer, same restore-before-source ordering as the backend's `dotnet restore`), then the rest of the source, then `npm run build` (`tsc -b && vite build`, per `frontend/package.json`). Also where `dist/version.json` is generated — see Versioning below.
2. **Runtime stage** — `nginx:1.27-alpine` (digest-pinned). Copies the Vite build output (`dist/`) and `docker/nginx.conf`. No Node, no build tooling, no `node_modules` in the shipped image.

**Alpine here, not Debian.** Unlike the backend (native SQLite interop makes musl a real risk, see `container-image.md#debian-not-alpine--for-now`), this image ships nothing but static files and nginx — no native Node addons, no runtime language interop to worry about on musl. Alpine's size win is a clean win here with none of the backend's caveats.

## TLS: nginx terminates it directly, same as Kestrel

Per [`container-image.md#tls-kestrel-terminates-it-directly`](./container-image.md#tls-kestrel-terminates-it-directly) and the project-wide "TLS from the start, everywhere" rule (`CLAUDE.md`), the frontend image is HTTPS-only end-to-end — no exception for "it's just static files." Same reasoning as the backend: no plaintext on the wire anywhere, including cluster-internal hops, and no ingress-terminated TLS.

- **Cert format: PEM, not PFX.** nginx consumes a plain cert/key pair natively (`ssl_certificate`/`ssl_certificate_key`) — no PKCS12 container needed, unlike Kestrel. `dotnet dev-certs https` (used for the backend's local `.pfx`) can also export PEM (`-ep ./certs/todo-app.crt --format PEM` support varies by SDK version) or a plain `openssl req` self-signed pair works equally well for local dev; either way the files are `todo-app.crt` / `todo-app.key`.
- **Mounted, never baked in** — same `/certs` convention as the backend. `docker run -v $(pwd)/frontend/certs:/certs:ro ...` locally; a K8s `Secret` volume-mounted at `/certs` in the cluster, same shape as the backend's cert `Secret` (see `container-image.md#cert-mounting` and `deployment.md`).
- **Port:** nginx listens on `8443` inside the container, matching the backend's port choice (avoids needing root to bind <1024, and keeps both services' "the HTTPS port is 8443" convention consistent). `EXPOSE 8443` declared, not published by default.

## Runtime user: non-root, chowned at build time

Runs as `nginx:alpine`'s built-in unprivileged `nginx` user (UID 101), same hardening stance as the backend's `app` user. One difference from the backend worth calling out because it wasn't obvious until tested:

**nginx's stock image normally drops privilege at container *startup*, while still running as root** — its default entrypoint chowns `/var/cache/nginx`'s subdirectories and creates the PID file at `/var/run/nginx.pid` before `exec`-ing into the worker processes as `nginx`. Setting `USER nginx` in the Dockerfile (so the *master* process itself never runs as root, not just the workers) skips that startup step entirely — verified locally: without an explicit fix, the container fails at boot with `mkdir() "/var/cache/nginx/client_temp" failed (13: Permission denied)`.

Fix: `chown -R nginx:nginx /var/cache/nginx /usr/share/nginx/html` and pre-create+chown `/var/run/nginx.pid`, all in a `RUN` step that still executes as root (before the `USER nginx` line) — done once at build time instead of relying on the base image's runtime privilege-drop.

## Serving: nginx config (`docker/nginx.conf`)

Three concerns, each its own `location` block:

1. **SPA fallback** (`location /`) — `try_files $uri $uri/ /index.html`. React Router's client-side routes need every path to resolve to `index.html` on a hard refresh or direct link, not just on in-app navigation; without this, refreshing on e.g. `/todos/3` 404s.
2. **Hashed asset caching** (`location /assets/`) — `Cache-Control: public, max-age=31536000, immutable`. Vite fingerprints these filenames with a content hash, so a new build always ships a new URL — safe to cache forever, a browser never needs to revalidate a URL it's already seen.
3. **`version.json` cache-busting** (`location = /version.json`) — explicit `Cache-Control: no-store`, the opposite of the assets rule. This file has a fixed name that doesn't change between builds (see Versioning below), so without this it could get stuck cached in a browser/proxy after a new image ships, defeating its whole purpose (checking what's actually deployed).

## Versioning: `dist/version.json`, the static-site equivalent of `GET /version`

Per [`versioning.md`](./versioning.md), the frontend uses the **same three build args** as the backend (`APP_VERSION`, `APP_COMMIT_SHA`, `APP_BUILD_DATE`), generated by `frontend-ci.yml`'s own `build-and-push` job calling `scripts/generate-version.sh web` — entirely independent of the backend's own version sequence (see [`versioning.md#per-service-patch-sequences`](./versioning.md#per-service-patch-sequences)), not a shared release number.

**Different mechanism from the backend, same shape.** The backend bakes these into the compiled .NET assembly (`AssemblyInformationalVersionAttribute`) and reads them back via a real endpoint (`GET /version`, `VersionEndpoint.cs`) at request time, because it has a server process to add a route to. This image is nginx serving a static build — there's no request-time code to run. Instead, the build stage writes a static file at a fixed path:

```
RUN printf '{"version":"%s","commit":"%s","buildDate":"%s"}' \
    "${APP_VERSION}" "${APP_COMMIT_SHA}" "${APP_BUILD_DATE}" \
    > dist/version.json
```

Same JSON shape as `VersionEndpoint.Response` (`{ version, commit, buildDate }`), served at the same conceptual path (`GET /version.json` here vs. `GET /version` on the backend — the `.json` extension because nginx is serving it as a literal static file, not routing it). `frontend-ci.yml` doesn't need to know this distinction — the same three build-arg names flow into its own `build-args`, same as `backend-ci.yml`'s.

**Local/no-build-args behavior:** matches the backend's fallback story — the Dockerfile defaults these args to `0.0.0-dev` / `unknown` / `unknown` if built without `--build-arg`, so a plain local `docker build -f docker/Dockerfile.app .` still produces a valid (if uninformative) `version.json`.

## `.dockerignore`

`frontend/node_modules/`, `frontend/dist/`, and `frontend/coverage/` are excluded from both images' shared build context (repo-root `.dockerignore`) — `node_modules` because `npm ci` inside the build stage installs its own copy against the container's platform/architecture (a host-installed `node_modules` could be the wrong arch, e.g. built on Apple Silicon but targeting `linux/amd64` in CI), and `dist`/`coverage` because they're build output, not input.

## Health check

Same reasoning as the backend (`container-image.md#health-check`) — no Docker `HEALTHCHECK` instruction; K8s liveness/readiness probes are the mechanism that matters once the cluster/ingress design exists (still TODO in `deployment.md`).

## Verified

`docker/Dockerfile.app` implements this spec and has been smoke-tested locally:

- Build succeeds; Vite production build and `version.json` generation both run correctly during the build stage.
- HTTPS via mounted self-signed cert/key confirmed working end-to-end (HTTP/2 negotiated over real TLS, same as the backend's Kestrel setup).
- Non-root `nginx` user confirmed (`id` inside the running container) — required the explicit `chown` fix described above; the naive `USER nginx` alone fails at container startup.
- `GET /version.json` returns the expected JSON shape with `Cache-Control: no-store`.
- SPA fallback confirmed — an arbitrary deep path (`/some/deep/route`) returns `200` (serving `index.html`), not a 404.

**Not yet verified** (needs a real push, not just a local build): the multi-arch manifest actually publishing correctly via `frontend-ci.yml`'s `build-and-push` job, and that `frontend-ci.yml`'s `paths:` filter actually keeps it from triggering at all on a backend-only push (and `backend-ci.yml`'s from triggering on a frontend-only push).

## Open Questions / TODO

- Local dev cert generation for the frontend isn't scripted anywhere yet (`frontend/certs/` exists but is empty) — a `openssl req` one-liner or a shared script with the backend's `dotnet dev-certs` step is a reasonable follow-up, not blocking.
- The three cert/PVC K8s-side items tracked in [`outstanding-items.md`](./outstanding-items.md) for the backend (cert rotation mechanics, resource limits, image vulnerability scanning) apply equally here once written up for two images instead of one.

## Related Docs

- [`container-image.md`](./container-image.md) — backend image design; shared conventions (digest pinning, multi-arch, non-root) covered there in full
- [`dockerfile-organization.md`](./dockerfile-organization.md) — where both Dockerfiles live, naming, the CI matrix
- [`versioning.md`](./versioning.md) — shared version numbering and build-arg wiring across both images
- [`deployment.md`](./deployment.md) — TLS strategy, deployment target
- [`../architecture/frontend/overview.md`](../architecture/frontend/overview.md) — frontend application design (React Router, no state library, generated API client)
