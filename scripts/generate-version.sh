#!/bin/sh
set -e

# Reads Major.Minor from VERSION, finds the latest matching git tag, and
# auto-increments the patch number.
#
# VERSION file contains "X.Y" (e.g. "1.0") and is hand-bumped when a
# major/minor release is intended. Patch is never hand-edited — it's
# derived from existing git tags matching "{Major.Minor}.*".

VERSION_FILE="VERSION"

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

LATEST_TAG=$(git tag -l "${MAJOR_MINOR}.*" --sort=-version:refname | head -n 1 || true)

if [ -z "$LATEST_TAG" ]; then
  PATCH=0
  echo "==> No existing tags found for ${MAJOR_MINOR}.x, starting at patch 0"
else
  CURRENT_PATCH=$(echo "$LATEST_TAG" | sed -E "s/^${MAJOR_MINOR}\.([0-9]+)$/\1/")
  PATCH=$((CURRENT_PATCH + 1))
  echo "==> Latest tag: $LATEST_TAG (patch: $CURRENT_PATCH)"
  echo "==> Incrementing to patch: $PATCH"
fi

VERSION="${MAJOR_MINOR}.${PATCH}"

echo "==> Generated version: $VERSION"

# GitHub Actions step output (requires $GITHUB_OUTPUT to be set by the runner)
if [ -n "$GITHUB_OUTPUT" ]; then
  echo "version=$VERSION" >> "$GITHUB_OUTPUT"
fi

echo ""
echo "========================================"
echo "Version Generation Complete"
echo "========================================"
echo "Major.Minor (from VERSION file): $MAJOR_MINOR"
echo "Patch (auto-incremented):        $PATCH"
echo "Full Version:                    $VERSION"
echo "========================================"
