/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

// Populated at container startup by docker/docker-entrypoint.sh (envsubst into
// public/env-config.js.template -> dist/env-config.js), not present in `npm run dev` —
// see docs/infra/container-image-frontend.md#runtime-api-base-url.
interface Window {
  __ENV__?: {
    API_BASE_URL: string
  }
}
