# Harbor Registry Setup

How to configure this repo to build and push its Docker images to Harbor, both locally and via CI. Two images share this same registry/project/robot-account setup — see [`dockerfile-organization.md`](./dockerfile-organization.md) for why they're separate images and how the Dockerfiles are laid out.

- **Registry:** `docker.thecameraeye.ca`
- **Project:** `todo-app`
- **Images:**
  - `docker.thecameraeye.ca/todo-app/todo-app-gateway` — backend, exists today
  - `docker.thecameraeye.ca/todo-app/todo-app-web` — frontend, not yet built (`frontend/` doesn't exist yet)

## Project visibility: public, dedicated project

**`todo-app` is a dedicated Harbor project created solely for this challenge's two images, with Access Level set to Public** (Harbor UI: Project → Configuration → "Public" checkbox) — anonymous `docker pull` works with no login, so a reviewer can pull either image without a Harbor account or robot credentials.

**Why a separate project rather than making an existing project public:** this registry also hosts other, unrelated projects (e.g. `inhouse`, used for other things on this home lab). Harbor's public/private toggle is a **project-level** setting, not per-repository — there is no way to expose just `todo-app-gateway` while keeping everything else under a shared project private. Putting these two images in their own project makes "public" and "public todo-app images only" the same statement, with no risk of an unrelated image becoming reachable later just because it landed in the same project.

**Push still requires the robot account below** — "Public" in Harbor only grants anonymous **pull**; write access is unaffected, so CI/local pushes are unchanged from before this was made public.

## Prerequisites

- Docker installed locally.
- Access to the `todo-app` project on `docker.thecameraeye.ca` (permission to create a robot account) — only needed for pushing; pulling needs nothing.
- `gh` CLI authenticated (already set up in this repo) if configuring GitHub Actions secrets from the command line.

## 1. Create a Harbor Robot Account

Robot accounts are Harbor's scoped, non-interactive credentials — use one instead of a personal Harbor login for both local Docker pushes and CI. Not needed for pulling, since the project is public (see above) — only for pushing new builds.

1. In the Harbor UI, go to the **todo-app** project → **Robot Accounts** → **New Robot Account**.
2. Scope it to the `todo-app` project, with at minimum **push** and **pull** permissions on repositories.
3. Save the generated **username** (typically `robot$todo-app+<name>`) and **token/secret** — the token is only shown once.

## 2. Local Docker Login

```bash
echo "<robot-token>" | docker login docker.thecameraeye.ca -u 'robot$todo-app+<robot-name>' --password-stdin
```

Credentials are stored in your local Docker config (`~/.docker/config.json`). Avoid passing the token via `-p` on the command line — it lands in shell history; the `--password-stdin` form above avoids that. **Only required for pushing** — since the project is public, `docker pull docker.thecameraeye.ca/todo-app/todo-app-gateway:latest` works with no login at all.

## 3. Local Build & Push

Each image is built with `-f docker/Dockerfile.<name>` against a repo-root context (see [`dockerfile-organization.md`](./dockerfile-organization.md)):

```bash
docker build -f docker/Dockerfile.gateway -t docker.thecameraeye.ca/todo-app/todo-app-gateway:latest .
docker push docker.thecameraeye.ca/todo-app/todo-app-gateway:latest
```

This single-platform form builds only for the host machine's architecture — fine for a quick local sanity check, but **it doesn't produce the same multi-arch manifest CI publishes** (see [`container-image.md#multi-arch-linuxamd64--linuxarm64`](./container-image.md#multi-arch-linuxamd64--linuxarm64)). To build/push both `linux/amd64` and `linux/arm64` locally, the same way CI does:

```bash
docker buildx build -f docker/Dockerfile.gateway --platform linux/amd64,linux/arm64 \
  -t docker.thecameraeye.ca/todo-app/todo-app-gateway:latest \
  --push .
```

(`--push` is required for a true multi-platform build — `buildx`'s `--load` only supports one platform at a time, matching the host.)

The same commands apply to the frontend with `-f docker/Dockerfile.app -t .../todo-app-web:latest`.

## 4. CI (GitHub Actions) Setup

Each service's own CI workflow builds and pushes its own image on a push to `main` that touches its paths — `backend-ci.yml`'s `build-and-push` job for `todo-app-gateway`, `frontend-ci.yml`'s for `todo-app-web` — using the registry/project values above (hardcoded in each workflow since they're fixed, not secret). See [`dockerfile-organization.md`](./dockerfile-organization.md#ci-one-workflow-per-service-path-filtered) for why this replaced an earlier single shared `docker-publish.yml` workflow. The same robot account credentials below are used by both workflows — no per-image secret needed.

It needs one **repository secret pair** (Settings → Secrets and variables → Actions → Secrets):

| Name | Value |
|---|---|
| `HARBOR_ROBOT_USERNAME` | `robot$todo-app+<robot-name>` |
| `HARBOR_ROBOT_TOKEN` | the robot account's token |

### Setting these via `gh` CLI

```bash
gh secret set HARBOR_ROBOT_USERNAME --body 'robot$todo-app+ci'
gh secret set HARBOR_ROBOT_TOKEN   # prompts for value, or pipe via stdin
```

## 5. Vulnerability Scanning

Resolves the "image vulnerability scanning" item tracked in [`outstanding-items.md`](./outstanding-items.md).

**Decision: use Harbor's built-in scanner**, not a dedicated scanning step in either CI workflow. Harbor ships with [Trivy](https://trivy.dev/) as its default native scanner — enabling it is a one-time project-level configuration change in the Harbor UI, not a CI/Dockerfile change. Since every image already lands on this same registry, there's no new service, secret, or workflow step to introduce; the scan happens where the image already is.

Considered and ruled out:

- **A dedicated CI scanning step** (Trivy or Grype run directly in `backend-ci.yml`/`frontend-ci.yml`, e.g. `aquasecurity/trivy-action`) — would duplicate what Harbor already does natively on the same image, adds another job/step to maintain in two places instead of one, and means findings live in CI logs instead of alongside the image in the registry where they're easiest to find later.
- **A separate scanning platform/SaaS** (Snyk, etc.) — unnecessary additional account/integration for a project this size; Harbor's native scanner already covers the actual need (OS packages + known application dependency CVEs) without it.

### Enabling the scanner on `todo-app`

1. In the Harbor UI (as an instance admin, not just a project member), go to **Administration → Interrogation Services**.
2. Confirm a Trivy scanner is listed and its status is **Healthy**. Most self-hosted Harbor instances ship with Trivy pre-registered at the instance level; this step just confirms it's active, it doesn't need setting up per project.
3. Go to the **todo-app** project → **Configuration**.
4. Under **Vulnerability Scanning**, enable **"Automatically scan images on push"** — this is a project-level setting, so it covers every repository under `todo-app` (both `todo-app-gateway` and `todo-app-web`) with no per-image configuration and no change needed to either CI workflow.

### How it runs

- **Trigger:** automatic, on every push — matches how `backend-ci.yml`/`frontend-ci.yml` each push their own image on a merge to `main` that touches their paths (see [`versioning.md`](./versioning.md)).
- **Scope:** covers every tag, including each platform-specific image inside the multi-arch manifest (see [`container-image.md#multi-arch-linuxamd64--linuxarm64`](./container-image.md#multi-arch-linuxamd64--linuxarm64)).
- **What it checks:** OS package vulnerabilities (from the Debian base image layers — see [`container-image.md#debian-not-alpine--for-now`](./container-image.md#debian-not-alpine--for-now)) and known-vulnerable application dependencies Trivy can detect from the image's installed packages/lockfiles.

### Reviewing results

1. Harbor UI → **todo-app** project → **Repositories** → `todo-app-gateway` (or `todo-app-web`) → select a tag.
2. The **Vulnerabilities** tab shows the scan report: severity breakdown (Critical/High/Medium/Low), CVE IDs, affected package, fixed-in version if available. A severity badge also shows directly on the repository/tag list view.

Via the API instead, using the same robot account credentials from step 1 above:

```bash
curl -u 'robot$todo-app+<robot-name>:<token>' \
  "https://docker.thecameraeye.ca/api/v2.0/projects/todo-app/repositories/todo-app-gateway/artifacts/latest/additions/vulnerabilities"
```

### Policy: informational only, not blocking the pipeline (for now)

Scanning does not currently fail either CI workflow's build or block a push. Harbor supports a **"Prevent vulnerable images from running"** project setting (blocks *pulling* above a configured severity) and a CI-side gate is also possible — neither is enabled today.

**Why deferred:** this is a take-home project's optional containerization enhancement, not a production pipeline with on-call response to a blocked deploy. A hard gate before the base Dockerfile has even shipped once would be premature — there's no baseline yet of what a "clean" scan looks like for this image. Revisit once the Dockerfile exists, a few real scans have run, and it's clear whether findings are actionable (e.g. a fixable OS package CVE) vs. noise (e.g. an unfixed CVE with no upstream patch yet).

## Related Docs

- [`dockerfile-organization.md`](./dockerfile-organization.md) — why there are two images, Dockerfile layout/naming, CI matrix structure
- [`container-image.md`](./container-image.md) — backend (`todo-app-gateway`) image design, including multi-arch (`linux/amd64`+`linux/arm64`) build/push
- [`deployment.md`](./deployment.md) — where these images get deployed (home lab Kubernetes), TLS strategy
- [`versioning.md`](./versioning.md) — how the semantic version tag (`:0.1.4`), shared across both images, is generated
- [`outstanding-items.md`](./outstanding-items.md) — deferred infra decisions; vulnerability scanning is resolved by section 5 above

## Notes

- If `docker.thecameraeye.ca` uses a self-signed or internal CA certificate, `docker login`/`docker push` will fail with an x509 error until the CA is trusted on the machine (locally) or the runner (in CI — GitHub-hosted runners can't reach a private/internal registry at all in that case, so a self-hosted runner with the CA installed would be required).
- The workflow tags every image with `latest`, the commit SHA (`github.sha`), and one shared generated semantic version (see [`versioning.md`](./versioning.md)) so specific builds remain addressable after `latest` moves.
- Each tag is a **multi-arch manifest list** covering `linux/amd64` and `linux/arm64` (see [`container-image.md`](./container-image.md#multi-arch-linuxamd64--linuxarm64)) — Docker/containerd on either architecture resolves the same tag to its matching platform image automatically, so there's no separate `-arm64`-suffixed tag to track.
- Containerization is listed as an **optional enhancement** in the challenge requirements (`docs/requirements/requirements.md`), not a core requirement — worth confirming this is a good use of time before/instead of core CRUD functionality and tests.
