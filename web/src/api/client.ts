import createClient from 'openapi-fetch'
import { apiFetch } from './http'
import type { paths } from './schema'

// Same origin as the page: the Vite proxy in development, the same host in production.
export const api = createClient<paths>({
  baseUrl: window.location.origin,
  credentials: 'same-origin',
  fetch: apiFetch,
})
