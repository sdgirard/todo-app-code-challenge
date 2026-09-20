import { describe, expect, it } from 'vitest'
import {
  getFieldErrors,
  getFirstFieldError,
  getMessage,
  isProblemDetails,
} from '../../src/lib/problemDetails'

const validationProblem = {
  title: 'One or more validation errors occurred.',
  status: 400,
  errors: { Title: ["'Title' must not be empty."] },
}

const plainProblem = {
  title: 'Not Found',
  status: 404,
  detail: 'Todo with the given id was not found.',
}

describe('isProblemDetails', () => {
  it('recognizes a well-formed Problem Details body', () => {
    expect(isProblemDetails(validationProblem)).toBe(true)
    expect(isProblemDetails(plainProblem)).toBe(true)
  })

  it('rejects a non-Problem-Details body', () => {
    expect(isProblemDetails(undefined)).toBe(false)
    expect(isProblemDetails(null)).toBe(false)
    expect(isProblemDetails('oops')).toBe(false)
    expect(isProblemDetails({ id: '123', title: 'a todo' })).toBe(false)
  })
})

describe('getFieldErrors', () => {
  it('extracts the field error dictionary from a ValidationProblemDetails body', () => {
    expect(getFieldErrors(validationProblem)).toEqual({
      Title: ["'Title' must not be empty."],
    })
  })

  it('returns an empty object when there is no errors field', () => {
    expect(getFieldErrors(plainProblem)).toEqual({})
    expect(getFieldErrors(undefined)).toEqual({})
  })
})

describe('getFirstFieldError', () => {
  it('returns the first message for a given field', () => {
    expect(getFirstFieldError(validationProblem, 'Title')).toBe(
      "'Title' must not be empty.",
    )
  })

  it('returns undefined for a field with no errors', () => {
    expect(getFirstFieldError(validationProblem, 'Description')).toBeUndefined()
  })
})

describe('getMessage', () => {
  it('prefers detail over title when both are present', () => {
    expect(getMessage(plainProblem)).toBe('Todo with the given id was not found.')
  })

  it('falls back to title when detail is absent', () => {
    expect(getMessage(validationProblem)).toBe(
      'One or more validation errors occurred.',
    )
  })

  it('returns undefined for a non-Problem-Details body', () => {
    expect(getMessage(undefined)).toBeUndefined()
  })
})
