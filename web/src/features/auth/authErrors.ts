import type { components } from '../../api/schema'

type Problem = components['schemas']['HttpValidationProblemDetails']

export type FormErrors<TField extends string> = {
  fields: Partial<Record<TField, string>>
  form?: string
}

export const INVALID_CREDENTIALS = 'The email or password is incorrect.'
export const UNEXPECTED_ERROR = 'Something went wrong. Please try again.'

export function toAuthFormErrors<TField extends string>(
  response: Response,
  problem: Problem | undefined,
  fields: readonly TField[],
): FormErrors<TField> {
  if (response.status === 401) {
    // Our own text, so the screen never hints whether the email exists.
    return { fields: {}, form: INVALID_CREDENTIALS }
  }

  if (response.status === 429) {
    return { fields: {}, form: tooManyAttempts(response.headers.get('Retry-After')) }
  }

  const result: FormErrors<TField> = { fields: {} }
  const unmatched: string[] = []

  for (const [key, messages] of Object.entries(problem?.errors ?? {})) {
    if (isField(key, fields)) {
      result.fields[key] = messages[0]
    } else {
      unmatched.push(...messages)
    }
  }

  if (unmatched.length > 0) {
    result.form = unmatched.join(' ')
  } else if (Object.keys(result.fields).length === 0) {
    result.form = problem?.detail ?? UNEXPECTED_ERROR
  }

  return result
}

function tooManyAttempts(retryAfter: string | null): string {
  const seconds = Number(retryAfter)
  return retryAfter !== null && Number.isFinite(seconds) && seconds > 0
    ? `Too many attempts. Try again in ${seconds} ${seconds === 1 ? 'second' : 'seconds'}.`
    : 'Too many attempts. Try again later.'
}

function isField<TField extends string>(key: string, fields: readonly TField[]): key is TField {
  return (fields as readonly string[]).includes(key)
}
