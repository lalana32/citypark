export const RETURN_PARAM = 'returnTo'

/** "/login" + "/lots/1?date=…" -> "/login?returnTo=%2Flots%2F1%3Fdate%3D…". */
export function withReturnTo(path: string, returnTo: string | null): string {
  return returnTo ? `${path}?${new URLSearchParams({ [RETURN_PARAM]: returnTo })}` : path
}
