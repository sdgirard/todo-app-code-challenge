export interface ValidationProblemDetails {
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}

export const isProblemDetails = (body: unknown): body is ValidationProblemDetails =>
  typeof body === 'object' &&
  body !== null &&
  'status' in body &&
  typeof (body as { status: unknown }).status === 'number'

export const getFieldErrors = (body: unknown): Record<string, string[]> => {
  if (!isProblemDetails(body) || !body.errors) return {}
  return body.errors
}

export const getFirstFieldError = (
  body: unknown,
  field: string,
): string | undefined => getFieldErrors(body)[field]?.[0]

export const getMessage = (body: unknown): string | undefined => {
  if (!isProblemDetails(body)) return undefined
  return body.detail ?? body.title
}
