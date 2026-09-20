const BASE_URL = import.meta.env.VITE_API_BASE_URL as string

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
