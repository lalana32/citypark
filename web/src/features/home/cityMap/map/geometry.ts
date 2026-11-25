export type Point = { x: number; y: number }

const fmt = (n: number) => Math.round(n * 100) / 100

const dist = (a: Point, b: Point) => Math.hypot(b.x - a.x, b.y - a.y)

const lerpTowards = (from: Point, to: Point, d: number): Point => {
  const len = dist(from, to)
  const t = len === 0 ? 0 : d / len
  return { x: from.x + (to.x - from.x) * t, y: from.y + (to.y - from.y) * t }
}

/** Cross product of the two segment directions; ~0 means the vertex is a straight pass-through. */
const isStraight = (a: Point, b: Point, c: Point) => {
  const v1x = b.x - a.x
  const v1y = b.y - a.y
  const v2x = c.x - b.x
  const v2y = c.y - b.y
  const cross = v1x * v2y - v1y * v2x
  const norm = Math.hypot(v1x, v1y) * Math.hypot(v2x, v2y)
  return norm === 0 || Math.abs(cross / norm) < 0.01
}

/**
 * Builds an SVG path through `points` with every corner rounded by a quadratic curve
 * of radius `radius` (clamped to half of the adjacent segments).
 *
 * Roads and routes both use this function with the same radius, so a route that passes
 * through a road's bend traces exactly the same curve as the drawn road.
 *
 * Closed paths start and end at the midpoint of the first segment — a straight stretch —
 * which keeps the loop seam invisible.
 */
export function roundedPath(points: Point[], radius: number, closed: boolean): string {
  const n = points.length
  if (n < 2) return ''

  const corner = (prev: Point, p: Point, next: Point) => {
    if (isStraight(prev, p, next)) return `L${fmt(p.x)} ${fmt(p.y)}`
    const r = Math.min(radius, dist(prev, p) / 2, dist(p, next) / 2)
    const a = lerpTowards(p, prev, r)
    const b = lerpTowards(p, next, r)
    return `L${fmt(a.x)} ${fmt(a.y)}Q${fmt(p.x)} ${fmt(p.y)} ${fmt(b.x)} ${fmt(b.y)}`
  }

  if (!closed) {
    let d = `M${fmt(points[0].x)} ${fmt(points[0].y)}`
    for (let i = 1; i < n - 1; i++) d += corner(points[i - 1], points[i], points[i + 1])
    d += `L${fmt(points[n - 1].x)} ${fmt(points[n - 1].y)}`
    return d
  }

  const start = lerpTowards(points[0], points[1], dist(points[0], points[1]) / 2)
  let d = `M${fmt(start.x)} ${fmt(start.y)}`
  for (let i = 1; i <= n; i++) {
    d += corner(points[i - 1], points[i % n], points[(i + 1) % n])
  }
  d += `L${fmt(start.x)} ${fmt(start.y)}Z`
  return d
}

/** Tiny deterministic PRNG so the procedural street fill is identical on every render. */
export function seededRandom(seed: number) {
  let s = seed >>> 0
  return () => {
    s = (s + 0x6d2b79f5) >>> 0
    let t = s
    t = Math.imul(t ^ (t >>> 15), t | 1)
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61)
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}
