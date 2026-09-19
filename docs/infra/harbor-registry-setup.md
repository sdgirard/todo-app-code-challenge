# Harbor Registry Setup

How to configure this repo to build and push its Docker image to Harbor, both locally and via CI.

- **Registry:** `docker.thecameraeye.ca`
- **Project:** `inhouse`
- **Image:** `docker.thecameraeye.ca/inhouse/todo-app`

## Prerequisites

- Docker installed locally.
- Access to the `inhouse` project on `docker.thecameraeye.ca` (permission to create a robot account).
- `gh` CLI authenticated (already set up in this repo) if configuring GitHub Actions secrets from the command line.

## 1. Create a Harbor Robot Account

Robot accounts are Harbor's scoped, non-interactive credentials — use one instead of a personal Harbor login for both local Docker pushes and CI.

1. In the Harbor UI, go to the **inhouse** project → **Robot Accounts** → **New Robot Account**.
2. Scope it to the `inhouse` project, with at minimum **push** and **pull** permissions on repositories.
3. Save the generated **username** (typically `robot$inhouse+<name>`) and **token/secret** — the token is only shown once.

## 2. Local Docker Login

```bash
echo "<robot-token>" | docker login docker.thecameraeye.ca -u 'robot$inhouse+<robot-name>' --password-stdin
```

Credentials are stored in your local Docker config (`~/.docker/config.json`). Avoid passing the token via `-p` on the command line — it lands in shell history; the `--password-stdin` form above avoids that.

## 3. Local Build & Push

Once a `Dockerfile` exists at the repo root:

```bash
docker build -t docker.thecameraeye.ca/inhouse/todo-app:latest .
docker push docker.thecameraeye.ca/inhouse/todo-app:latest
```

## 4. CI (GitHub Actions) Setup

The workflow at `.github/workflows/docker-publish.yml` builds and pushes to Harbor on every push to `main`, using the registry/project values above (hardcoded in the workflow since they're fixed, not secret).

It needs one **repository secret pair** (Settings → Secrets and variables → Actions → Secrets):

| Name | Value |
|---|---|
| `HARBOR_ROBOT_USERNAME` | `robot$inhouse+<robot-name>` |
| `HARBOR_ROBOT_TOKEN` | the robot account's token |

### Setting these via `gh` CLI

```bash
gh secret set HARBOR_ROBOT_USERNAME --body 'robot$inhouse+ci'
gh secret set HARBOR_ROBOT_TOKEN   # prompts for value, or pipe via stdin
```

## Related Docs

- [`deployment.md`](./deployment.md) — where this image gets deployed (home lab Kubernetes), TLS strategy
- [`versioning.md`](./versioning.md) — how the semantic version tag (`:0.1.4`) applied to these images is generated

## Notes

- If `docker.thecameraeye.ca` uses a self-signed or internal CA certificate, `docker login`/`docker push` will fail with an x509 error until the CA is trusted on the machine (locally) or the runner (in CI — GitHub-hosted runners can't reach a private/internal registry at all in that case, so a self-hosted runner with the CA installed would be required).
- The workflow tags images with `latest`, the commit SHA (`github.sha`), and a generated semantic version (see [`versioning.md`](./versioning.md)) so specific builds remain addressable after `latest` moves.
- Containerization is listed as an **optional enhancement** in the challenge requirements (`docs/requirements/requirements.md`), not a core requirement — worth confirming this is a good use of time before/instead of core CRUD functionality and tests.
