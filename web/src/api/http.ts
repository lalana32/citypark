import type { components } from './schema'

export const CSRF_HEADER = 'X-XSRF-TOKEN'

const CSRF_PATH = '/api/v1/auth/csrf'
const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS', 'TRACE'])

type CsrfTokenResponse = components['schemas']['CsrfTokenResponse']

/**
 * Adds the antiforgery token to every unsafe request. The token is fetched fresh each time
 * and only kept in memory: it is bound to the signed-in user, so it changes on login and logout.
 */
export async function apiFetch(request: Request): Promise<Response> {
  if (!SAFE_METHODS.has(request.method)) {
    request.headers.set(CSRF_HEADER, await fetchCsrfToken())
  }

  return fetch(request)
}

async function fetchCsrfToken(): Promise<string> {
  const response = await fetch(new URL(CSRF_PATH, window.location.origin), { credentials: 'same-origin' })
  if (!response.ok) {
    throw new Error(`The CSRF token request failed with ${response.status}.`)
  }

  const { token } = (await response.json()) as CsrfTokenResponse
  return token
}
