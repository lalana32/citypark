import { seededRandom, type Point } from './geometry'
import { NODES, type NodeId } from './network'

/**
 * DISTRICTS
 * ---------
 * Each district is a polygon (usually the area between major roads) that gets filled
 * with hairline local streets. They are purely visual — routes never use them.
 *
 * kinds:
 *  - grid   regular street grid, optionally rotated (`angle`) and with random gaps (`drop`)
 *  - lots   irregular recursive subdivision (suburban / industrial parcels)
 *  - blocks solid building footprints
 *  - empty  nothing (large dark superblocks give the composition breathing room)
 */

type Vertex = NodeId | Point

interface BaseDistrict {
  id: string
  poly: Vertex[]
  seed: number
  /** Subtle area tint (0–1), used sparingly to suggest parks / campuses. */
  tint?: number
}

export type District =
  | (BaseDistrict & { kind: 'grid'; sx: number; sy: number; drop?: number; angle?: number })
  | (BaseDistrict & { kind: 'lots'; minSize: number; depth: number })
  | (BaseDistrict & { kind: 'blocks'; size: number; gap: number; fill?: number })
  | (BaseDistrict & { kind: 'empty' })

const r = (x0: number, y0: number, x1: number, y1: number): Point[] => [
  { x: x0, y: y0 },
  { x: x1, y: y0 },
  { x: x1, y: y1 },
  { x: x0, y: y1 },
]

export const DISTRICTS: District[] = [
  // ── North strip ──────────────────────────────────────────────
  { id: 'n-west', kind: 'lots', poly: r(-620, -1100, 230, 140), minSize: 40, depth: 6, seed: 11 },
  { id: 'n-mid-w', kind: 'grid', poly: r(230, -1100, 518, 140), sx: 46, sy: 34, drop: 0.25, seed: 12 },
  { id: 'n-mid', kind: 'lots', poly: r(520, -1100, 990, 140), minSize: 80, depth: 5, seed: 13 },
  { id: 'n-east', kind: 'grid', poly: r(990, -1100, 1230, 140), sx: 30, sy: 26, drop: 0.12, seed: 14 },
  { id: 'n-far-east', kind: 'lots', poly: r(1230, -1100, 2220, 140), minSize: 36, depth: 7, seed: 15 },

  // ── Upper band ───────────────────────────────────────────────
  { id: 'w-upper', kind: 'lots', poly: r(-620, 140, 230, 390), minSize: 44, depth: 6, seed: 21 },
  { id: 'nw-grid', kind: 'grid', poly: ['A1', 'B1', 'BS1', 'AS1'], sx: 40, sy: 26, drop: 0.2, seed: 22 },
  { id: 'nw-blocks', kind: 'blocks', poly: ['AS1', 'BS1', 'B2', 'A2'], size: 20, gap: 12, fill: 0.5, seed: 23 },
  { id: 'diag-north', kind: 'grid', poly: ['B1', 'D1', 'D2', 'X1'], sx: 34, sy: 30, drop: 0.18, seed: 24 },
  { id: 'diag-west', kind: 'grid', poly: ['B1', 'X1', 'B2'], sx: 26, sy: 34, drop: 0.1, angle: 43.8, seed: 25 },
  { id: 'cbd-north', kind: 'grid', poly: ['D1', 'K1', 'K2', 'D2'], sx: 24, sy: 24, drop: 0.05, seed: 26 },
  { id: 'k-north', kind: 'grid', poly: ['K1', 'E1', 'E2', 'K2'], sx: 30, sy: 25, drop: 0.08, seed: 27 },
  { id: 'ne-lots', kind: 'lots', poly: ['E1', 'F1', 'FNE', 'N2', 'E2'], minSize: 30, depth: 5, seed: 28 },
  { id: 'ne-far', kind: 'grid', poly: ['F1', 'H1e', 'NEe', 'FNE'], sx: 22, sy: 36, drop: 0.3, seed: 29 },
  { id: 'ne-sliver', kind: 'empty', poly: ['FNE', 'NEe', 'H2e', 'F2'], seed: 30 },

  // ── Middle band ──────────────────────────────────────────────
  { id: 'w-mid', kind: 'empty', poly: r(-620, 390, 230, 590), seed: 31 },
  { id: 'park', kind: 'empty', poly: ['A2', 'B2', 'B3', 'A3'], tint: 0.05, seed: 32 },
  { id: 'c-west', kind: 'grid', poly: ['B2', 'C2', 'C3', 'B3'], sx: 22, sy: 25, drop: 0.06, seed: 33 },
  { id: 'downtown', kind: 'grid', poly: ['C2', 'X1', 'D3', 'C3'], sx: 22, sy: 22, drop: 0.04, seed: 34 },
  { id: 'diag-east', kind: 'grid', poly: ['X1', 'D2', 'D3'], sx: 22, sy: 28, drop: 0.08, angle: 43.8, seed: 35 },
  { id: 'cbd-mid', kind: 'grid', poly: ['D2', 'K2', 'KNE', 'D3'], sx: 24, sy: 22, drop: 0.06, seed: 36 },
  { id: 'k-mid', kind: 'grid', poly: ['K2', 'E2', 'ENE', 'KNE'], sx: 30, sy: 24, drop: 0.1, seed: 37 },
  { id: 'ne-tilted', kind: 'grid', poly: ['ENE', 'N2', 'F2', 'F3', 'E3'], sx: 20, sy: 30, drop: 0.08, angle: -35.5, seed: 38 },
  { id: 'e-mid', kind: 'lots', poly: ['F2', 'H2e', 'H3e', 'F3'], minSize: 36, depth: 4, seed: 39 },

  // ── Lower band ───────────────────────────────────────────────
  { id: 'w-low', kind: 'lots', poly: r(-620, 590, 230, 780), minSize: 44, depth: 5, seed: 41 },
  { id: 'sw-park', kind: 'empty', poly: ['A3', 'B3', 'B4', 'A4'], tint: 0.045, seed: 42 },
  { id: 'c-low', kind: 'grid', poly: ['B3', 'C3', 'C4', 'B4'], sx: 24, sy: 22, drop: 0.08, seed: 43 },
  { id: 'downtown-s', kind: 'grid', poly: ['C3', 'D3', 'D4', 'C4'], sx: 22, sy: 24, drop: 0.05, seed: 44 },
  { id: 'e-low', kind: 'grid', poly: ['D3', 'K3', 'E3', 'E4', 'D4'], sx: 26, sy: 30, drop: 0.1, seed: 45 },
  { id: 'e-strips', kind: 'grid', poly: ['E3', 'F3', 'F4', 'E4'], sx: 14, sy: 60, drop: 0.12, seed: 46 },
  { id: 'far-e-low', kind: 'lots', poly: ['F3', 'H3e', 'H4e', 'F4'], minSize: 34, depth: 5, seed: 47 },

  // ── South band ───────────────────────────────────────────────
  { id: 'sw-lots', kind: 'lots', poly: r(-620, 780, 230, 920), minSize: 36, depth: 6, seed: 51 },
  { id: 's-grid-w', kind: 'grid', poly: ['A4', 'B4', 'B5', 'A5'], sx: 34, sy: 28, drop: 0.22, seed: 52 },
  { id: 's-grid-c', kind: 'grid', poly: ['B4', 'C4', 'C5', 'B5'], sx: 22, sy: 24, drop: 0.08, seed: 53 },
  { id: 's-grid-d', kind: 'grid', poly: ['C4', 'D4', 'D5', 'C5'], sx: 26, sy: 26, drop: 0.1, seed: 54 },
  { id: 's-strips', kind: 'grid', poly: ['D4', 'F4', 'F5', 'D5'], sx: 16, sy: 70, drop: 0.2, seed: 55 },
  { id: 's-far-e', kind: 'empty', poly: ['F4', 'H4e', 'FWe', 'F5'], seed: 56 },
  { id: 'south-edge', kind: 'lots', poly: r(-620, 920, 2220, 2100), minSize: 50, depth: 8, seed: 57 },
]

export const resolvePoly = (poly: Vertex[]): Point[] =>
  poly.map((v) => (typeof v === 'string' ? NODES[v] : v))

const bbox = (pts: Point[]) => {
  const xs = pts.map((p) => p.x)
  const ys = pts.map((p) => p.y)
  return { x0: Math.min(...xs), y0: Math.min(...ys), x1: Math.max(...xs), y1: Math.max(...ys) }
}

const f = (n: number) => Math.round(n * 10) / 10

/** Hairline street geometry for a district, as one SVG path `d` (cheap to render). */
export function districtStreets(d: District): string {
  const pts = resolvePoly(d.poly)
  const b = bbox(pts)
  const rand = seededRandom(d.seed)
  let out = ''

  if (d.kind === 'grid') {
    const angle = ((d.angle ?? 0) * Math.PI) / 180
    const cos = Math.cos(angle)
    const sin = Math.sin(angle)
    const cx = (b.x0 + b.x1) / 2
    const cy = (b.y0 + b.y1) / 2
    // Cover the bbox diagonal so rotated grids still fill the polygon (clipPath trims the rest).
    const half = Math.hypot(b.x1 - b.x0, b.y1 - b.y0) / 2 + 10
    const tx = (u: number, v: number) => `${f(cx + u * cos - v * sin)} ${f(cy + u * sin + v * cos)}`
    const drop = d.drop ?? 0
    const offU = rand() * d.sx
    const offV = rand() * d.sy
    for (let u = -half + offU; u <= half; u += d.sx) {
      for (let v = -half + offV; v < half; v += d.sy) {
        if (rand() >= drop) out += `M${tx(u, v)}L${tx(u, v + d.sy)}`
      }
    }
    for (let v = -half + offV; v <= half; v += d.sy) {
      for (let u = -half + offU; u < half; u += d.sx) {
        if (rand() >= drop) out += `M${tx(u, v)}L${tx(u + d.sx, v)}`
      }
    }
  } else if (d.kind === 'lots') {
    const split = (x0: number, y0: number, x1: number, y1: number, depth: number) => {
      const w = x1 - x0
      const h = y1 - y0
      if (depth <= 0 || (w < d.minSize * 2 && h < d.minSize * 2)) return
      const vertical = w > h ? rand() < 0.8 : rand() < 0.2
      const t = 0.3 + rand() * 0.4
      // Some splits stop short, producing dead-end streets.
      const partial = rand() < 0.25 ? 0.45 + rand() * 0.4 : 1
      if (vertical && w >= d.minSize * 2) {
        const x = x0 + w * t
        out += `M${f(x)} ${f(y0)}L${f(x)} ${f(y0 + h * partial)}`
        split(x0, y0, x, y1, depth - 1)
        split(x, y0, x1, y1, depth - 1)
      } else if (h >= d.minSize * 2) {
        const y = y0 + h * t
        out += `M${f(x0)} ${f(y)}L${f(x0 + w * partial)} ${f(y)}`
        split(x0, y0, x1, y, depth - 1)
        split(x0, y, x1, y1, depth - 1)
      }
    }
    split(b.x0, b.y0, b.x1, b.y1, d.depth)
  } else if (d.kind === 'blocks') {
    const step = d.size + d.gap
    for (let y = b.y0 + d.gap; y + d.size <= b.y1 - d.gap / 2; y += step) {
      for (let x = b.x0 + d.gap; x + d.size <= b.x1 - d.gap / 2; x += step) {
        if (rand() > (d.fill ?? 1)) continue
        // Occasionally merge with the next cell into a wider footprint.
        const wide = rand() < 0.3 && x + step + d.size <= b.x1 - d.gap / 2
        const w = wide ? d.size + step : d.size
        out += `M${f(x)} ${f(y)}h${f(w)}v${d.size}h${f(-w)}Z`
        if (wide) x += step
      }
    }
  }
  return out
}

export const polyPoints = (d: District) =>
  resolvePoly(d.poly)
    .map((p) => `${p.x},${p.y}`)
    .join(' ')
