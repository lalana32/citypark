export const RETURN_PARAM = 'returnTo'

/** "/login" + "/lots/1?date=…" -> "/login?returnTo=%2Flots%2F1%3Fdate%3D…". */
export function withReturnTo(path: string, returnTo: string | null): string {
  return returnTo ? `${path}?${new URLSearchParams({ [RETURN_PARAM]: returnTo })}` : path
}

// Any real origin works: only "is it still this origin?" matters.
const SAME_SITE = 'https://citypark.invalid'

/** The ?returnTo= path when it stays on this site; null for a missing or external target (no open redirect). */
export function readReturnTo(params: URLSearchParams): string | null {
  const value = params.get(RETURN_PARAM)
  if (!value?.startsWith('/')) {
    return null
  }
  try {
    // The URL parser catches what a prefix check misses, such as "//evil.com" or "/\evil.com".
    const url = new URL(value, SAME_SITE)
    return url.origin === SAME_SITE ? url.pathname + url.search + url.hash : null
  } catch {
    return null
  }
}
