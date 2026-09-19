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

1. **`version`** — checks out full git history (`fetch-depth: 0`, needed to see all existing tags), runs `generate-version.sh`, exposes the computed version as a job output.
2. **`build-and-push`** — builds and pushes the Docker image to Harbor, tagged with `latest`, the commit SHA, *and* the generated semantic version (`docker.thecameraeye.ca/inhouse/todo-app:0.1.4`). Also stamps `org.opencontainers.image.version`/`.revision`/`.source` OCI labels on the image.
3. **`version-tag`** — only runs after a successful build+push, creates an annotated git tag (`0.1.4`) and pushes it back to the repo, using the workflow's own `GITHUB_TOKEN` (via `permissions: contents: write`) — no separate secret to create.

If the build/push fails, no tag is created — the tag only exists for versions that actually got published.

## Related Docs

- [`harbor-registry-setup.md`](./harbor-registry-setup.md) — where these images get pushed
- [`deployment.md`](./deployment.md) — deployment target
