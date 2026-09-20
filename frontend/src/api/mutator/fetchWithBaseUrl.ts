// Runtime config (window.__ENV__, from docker/docker-entrypoint.sh's envsubst step) takes
// precedence over the build-time Vite env var, so one built container image can point at
// different backend URLs per environment without a rebuild — see
// docs/infra/container-image-frontend.md#runtime-api-base-url. `npm run dev`/a bare static
// `dist/` never gets window.__ENV__ (no entrypoint to generate it), so it falls back to the
// Vite env var exactly as before.
const BASE_URL = window.__ENV__?.API_BASE_URL ?? (import.meta.env.VITE_API_BASE_URL as string)

const parseBody = (body: string) => {
  try {
    return JSON.parse(body)
  } catch {
    return undefined
  }
}

export const fetchWithBaseUrl = async <T>(url: string, options?: RequestInit): Promise<T> => {
  const res = await fetch(`${BASE_URL}${url}`, options)
  const body = [204, 205, 304].includes(res.status) ? null : await res.text()
  const data = body ? parseBody(body) : undefined

  return { data, status: res.status, headers: res.headers } as T
}
