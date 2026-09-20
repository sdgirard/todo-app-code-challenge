# Frontend SPA container image.
# Design decisions and rationale: docs/infra/container-image-frontend.md — read that before changing this file.
# Build context is the repo root (not docker/) — see docs/infra/dockerfile-organization.md.
#
# Build:
#   docker build -f docker/Dockerfile.app -t todo-app-web .
#
# Multi-arch build/push (matches CI):
#   docker buildx build -f docker/Dockerfile.app --platform linux/amd64,linux/arm64 -t docker.thecameraeye.ca/todo-app/todo-app-web:latest --push .

# ---- Build stage -----------------------------------------------------------
FROM node:22-alpine@sha256:b6f26b36c8ff49624cfdac716b8ea1138d606df02586a77d364bb5536a634f85 AS build

# Version info, passed by docker-publish.yml — same three build args as
# docker/Dockerfile.gateway, see docs/infra/versioning.md. Defaults keep a
# plain local `docker build` (no --build-arg) working.
ARG APP_VERSION=0.0.0-dev
ARG APP_COMMIT_SHA=unknown
ARG APP_BUILD_DATE=unknown

WORKDIR /src

# Copy only the manifest/lockfile first so `npm ci` is its own cached layer —
# it only re-runs when a dependency changes, not on every source edit.
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

# Now bring in the rest of the source — this layer changes on nearly every
# build, so it's kept after the expensive install step above.
COPY frontend/ .

RUN npm run build

# version.json is the static-site equivalent of the backend's GET /version —
# there's no server-side app code here for a real route, so this is a plain
# file nginx serves as-is. Same {version, commit, buildDate} shape as
# VersionEndpoint.cs's Response record, generated from the same build args.
RUN printf '{"version":"%s","commit":"%s","buildDate":"%s"}' \
        "${APP_VERSION}" "${APP_COMMIT_SHA}" "${APP_BUILD_DATE}" \
        > dist/version.json

# ---- Runtime stage ----------------------------------------------------------
FROM nginx:1.27-alpine@sha256:65645c7bb6a0661892a8b03b89d0743208a18dd2f3f17a54ef4b76fb8e2f2a10 AS runtime

COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist /usr/share/nginx/html

# Cert is mounted at runtime, never baked into the image — same convention as
# docker/Dockerfile.gateway, see docs/infra/container-image-frontend.md#tls.
# PEM format (not .pfx) since nginx, unlike Kestrel, doesn't consume PKCS12.
EXPOSE 8443

# nginx:alpine ships a built-in unprivileged `nginx` user, but its stock
# entrypoint only chowns/creates /var/cache/nginx's subdirectories and the
# PID file at container startup while still running as root, then drops to
# `nginx` for the worker processes. Running the master process itself as
# `nginx` from the start (USER below, no root step in between) skips that
# runtime privilege-drop entirely, so those paths need to be writable by
# `nginx` ahead of time instead — done here at build time, while still root,
# rather than relying on the base image's own startup behavior.
RUN chown -R nginx:nginx /var/cache/nginx /usr/share/nginx/html && \
    touch /var/run/nginx.pid && \
    chown nginx:nginx /var/run/nginx.pid
USER nginx

ENTRYPOINT ["nginx", "-g", "daemon off;"]
