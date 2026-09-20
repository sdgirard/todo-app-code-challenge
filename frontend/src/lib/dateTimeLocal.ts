// Converts between the browser's local-time wall-clock string that
// <input type="datetime-local"> produces/accepts (e.g. "2026-09-26T14:30",
// no timezone offset) and the UTC ISO 8601 string the backend stores and
// returns (e.g. "2026-09-26T18:30:00.000Z"). The input's value always means
// "this local time in whatever timezone the browser is running in" — never UTC.

// Converts a UTC ISO 8601 string (or null) into the local-time string shape
// <input type="datetime-local"> expects as its value.
export const toDateTimeLocal = (utcIsoString: string | null): string => {
  if (!utcIsoString) return ''
  const date = new Date(utcIsoString)
  if (Number.isNaN(date.getTime())) return ''
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

// Converts a <input type="datetime-local"> value (local wall-clock time, no
// offset) into a UTC ISO 8601 string for the backend. `new Date(...)` parses
// an offset-less "YYYY-MM-DDTHH:mm" as local time per the spec, so
// `.toISOString()` on the result is the correct UTC conversion.
export const fromDateTimeLocal = (localValue: string): string | null => {
  if (!localValue) return null
  const date = new Date(localValue)
  if (Number.isNaN(date.getTime())) return null
  return date.toISOString()
}
