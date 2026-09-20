#!/bin/sh
set -e

# Generates dist/env-config.js from env-config.js.template at container startup
# (not build time) so one built todo-app-web image can point at a different
# backend URL per environment without a rebuild — see
# docs/infra/container-image-frontend.md#runtime-api-base-url.
#
# API_BASE_URL defaults to empty (relative /todos, /todos/{id}, ...) so a
# deployment that fronts both images behind one reverse proxy/origin still
# works with no env var set at all — only a split-origin setup (e.g. this
# project's docker-compose.yml, each image on its own published port) needs
# to set it.

: "${API_BASE_URL:=}"
envsubst '${API_BASE_URL}' < /usr/share/nginx/html/env-config.js.template > /usr/share/nginx/html/env-config.js

exec "$@"
