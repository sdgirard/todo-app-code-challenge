#!/bin/sh
set -e

# Generates the two certs docker-compose.yml / docker-compose.registry.yml need to
# run the backend (Kestrel, PFX) and frontend (nginx, PEM) images over HTTPS locally
# — see docs/infra/container-image.md#cert-format-pfxpkcs12 and
# docs/infra/container-image-frontend.md#tls-nginx-terminates-it-directly-same-as-kestrel
# for why the two images need different cert formats in the first place.
#
# Idempotent: skips a cert that already exists rather than overwriting it, so
# re-running this after the first setup (or after a `git clean`) is safe.
#
# Usage: scripts/setup-compose-certs.sh
# Then:  export CERT_PASSWORD=<password this script prints>
#        docker compose up --build

BACKEND_CERT_DIR="certs"
BACKEND_PFX="$BACKEND_CERT_DIR/todo-app.pfx"
FRONTEND_CERT_DIR="frontend/certs"
FRONTEND_CRT="$FRONTEND_CERT_DIR/todo-app.crt"
FRONTEND_KEY="$FRONTEND_CERT_DIR/todo-app.key"

mkdir -p "$BACKEND_CERT_DIR" "$FRONTEND_CERT_DIR"

if [ -f "$BACKEND_PFX" ]; then
  echo "Backend cert already exists at $BACKEND_PFX — skipping."
  echo "(If you don't know its password, delete it and re-run this script.)"
else
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "ERROR: dotnet SDK not found — required to generate the backend .pfx cert." >&2
    exit 1
  fi
  CERT_PASSWORD=$(openssl rand -base64 24)
  dotnet dev-certs https -ep "$BACKEND_PFX" -p "$CERT_PASSWORD" >/dev/null
  echo "Generated backend cert: $BACKEND_PFX"
fi

if [ -f "$FRONTEND_CRT" ] && [ -f "$FRONTEND_KEY" ]; then
  echo "Frontend cert already exists at $FRONTEND_CRT / $FRONTEND_KEY — skipping."
else
  if ! command -v openssl >/dev/null 2>&1; then
    echo "ERROR: openssl not found — required to generate the frontend cert/key pair." >&2
    exit 1
  fi
  openssl req -x509 -newkey rsa:2048 -nodes \
    -keyout "$FRONTEND_KEY" -out "$FRONTEND_CRT" \
    -days 365 -subj "/CN=localhost" 2>/dev/null
  echo "Generated frontend cert: $FRONTEND_CRT / $FRONTEND_KEY"
fi

echo
if [ -n "$CERT_PASSWORD" ]; then
  echo "Backend .pfx password (save this — it's not stored anywhere):"
  echo
  echo "    export CERT_PASSWORD=$CERT_PASSWORD"
  echo
  echo "Then run: docker compose up --build"
else
  echo "Backend cert already existed, so no new password was generated."
  echo "Set CERT_PASSWORD to whatever password that .pfx was created with, then:"
  echo "    docker compose up --build"
fi
