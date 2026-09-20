#!/bin/sh
set -e

# Reads Major.Minor from VERSION, finds the latest matching git tag for the
# given service prefix, and auto-increments the patch number.
#
# VERSION file contains "X.Y" (e.g. "1.0") and is hand-bumped when a
# major/minor release is intended, shared by every service. Patch is never
# hand-edited — it's derived from existing git tags matching
# "{prefix}-{Major.Minor}.*", scoped per service so the backend and frontend
# each have their own independent patch sequence (see
# docs/infra/versioning.md#per-service-patch-sequences) — the two workflows
# building/pushing independently means there's no single shared "next patch"
# to coordinate across them.
#
# Usage: generate-version.sh <prefix>   e.g. generate-version.sh gateway

VERSION_FILE="VERSION"
PREFIX="$1"

if [ -z "$PREFIX" ]; then
  echo "ERROR: usage: $0 <prefix>  (e.g. $0 gateway)"
  exit 1
fi

if [ ! -f "$VERSION_FILE" ]; then
  echo "ERROR: VERSION file not found at $VERSION_FILE"
  exit 1
fi

MAJOR_MINOR=$(cat "$VERSION_FILE" | tr -d '\n\r ')
echo "==> Major.Minor version from VERSION file: $MAJOR_MINOR"

if ! echo "$MAJOR_MINOR" | grep -qE '^[0-9]+\.[0-9]+$'; then
  echo "ERROR: VERSION file must contain format 'X.Y' (e.g., '1.0')"
  echo "       Current content: '$MAJOR_MINOR'"
  exit 1
fi

TAG_PATTERN="${PREFIX}-${MAJOR_MINOR}.*"
LATEST_TAG=$(git tag -l "$TAG_PATTERN" --sort=-version:refname | head -n 1 || true)

if [ -n "$LATEST_TAG" ]; then
  CURRENT_PATCH=$(echo "$LATEST_TAG" | sed -E "s/^${PREFIX}-${MAJOR_MINOR}\.([0-9]+)$/\1/")
  PATCH=$((CURRENT_PATCH + 1))
  echo "==> Latest tag: $LATEST_TAG (patch: $CURRENT_PATCH)"
  echo "==> Incrementing to patch: $PATCH"
elif [ "$PREFIX" = "gateway" ] && LEGACY_TAG=$(git tag -l "${MAJOR_MINOR}.*" --sort=-version:refname | head -n 1) && [ -n "$LEGACY_TAG" ]; then
  # One-time compatibility path: before the frontend existed, the gateway's
  # releases were tagged without a prefix (0.1.0-0.1.11). Continue that
  # sequence under the gateway- prefix rather than restarting at .0, which
  # would collide with an image tag already pushed to Harbor. Drop this
  # branch once every environment has moved past the legacy tags.
  CURRENT_PATCH=$(echo "$LEGACY_TAG" | sed -E "s/^${MAJOR_MINOR}\.([0-9]+)$/\1/")
  PATCH=$((CURRENT_PATCH + 1))
  echo "==> No gateway-prefixed tag found; continuing from legacy tag: $LEGACY_TAG (patch: $CURRENT_PATCH)"
  echo "==> Incrementing to patch: $PATCH"
else
  PATCH=0
  echo "==> No existing tags found matching ${TAG_PATTERN}, starting at patch 0"
fi

VERSION="${MAJOR_MINOR}.${PATCH}"
GIT_TAG="${PREFIX}-${VERSION}"

echo "==> Generated version: $VERSION (git tag: $GIT_TAG)"

# GitHub Actions step output (requires $GITHUB_OUTPUT to be set by the runner)
if [ -n "$GITHUB_OUTPUT" ]; then
  echo "version=$VERSION" >> "$GITHUB_OUTPUT"
  echo "git_tag=$GIT_TAG" >> "$GITHUB_OUTPUT"
fi

echo ""
echo "========================================"
echo "Version Generation Complete"
echo "========================================"
echo "Prefix:                          $PREFIX"
echo "Major.Minor (from VERSION file): $MAJOR_MINOR"
echo "Patch (auto-incremented):        $PATCH"
echo "Full Version:                    $VERSION"
echo "Git Tag:                         $GIT_TAG"
echo "========================================"
