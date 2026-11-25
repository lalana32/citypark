import type { Point } from './geometry'

/**
 * THE STREET NETWORK
 * ------------------
 * The map is drawn in a fixed 1600 × 1000 coordinate space (see MAP_WIDTH / MAP_HEIGHT)
 * and scaled responsively by the SVG viewBox.
 *
 * - `NODES` are named points: intersections, bends and off-canvas road ends. Road ends lie
 *   far outside 1600 × 1000 so the map never looks cut off when the frame has to zoom out.
 * - `ROADS` are polylines through those nodes. Every road crossing MUST be a shared node,
 *   otherwise the routes cannot turn there (the dev validator warns about this).
 * - Routes (see src/animation/config.ts) are lists of node names. Consecutive nodes must be
 *   neighbours on some road, which guarantees that markers never leave the streets.
 */

export const MAP_WIDTH = 1600
export const MAP_HEIGHT = 1000

/** Corner radius shared by road bends and route turns (keeps markers exactly on bends). */
export const CORNER_RADIUS = 18

export const NODES = {
  // ── H1 · y=140 ────────────────────────────────
  H1w: { x: -620, y: 140 },
  A1: { x: 230, y: 140 },
  B1: { x: 520, y: 140 },
  D1: { x: 990, y: 140 },
  K1: { x: 1110, y: 140 },
  E1: { x: 1230, y: 140 },
  F1: { x: 1440, y: 140 },
  H1e: { x: 2220, y: 140 },
  // ── S1 · y=270 (west street) ──────────────────
  S1w: { x: -620, y: 270 },
  AS1: { x: 230, y: 270 },
  BS1: { x: 521, y: 270 },
  // ── H2 · y=390 ────────────────────────────────
  H2w: { x: -620, y: 390 },
  A2: { x: 230, y: 390 },
  B2: { x: 522, y: 390 },
  C2: { x: 700, y: 390 },
  X1: { x: 781, y: 390 },
  D2: { x: 990, y: 390 },
  K2: { x: 1110, y: 390 },
  E2: { x: 1230, y: 390 },
  N2: { x: 1356, y: 390 },
  F2: { x: 1440, y: 390 },
  H2e: { x: 2220, y: 390 },
  // ── H3 · y=590 (main street) ──────────────────
  H3w: { x: -620, y: 590 },
  A3: { x: 230, y: 590 },
  B3: { x: 526, y: 590 },
  C3: { x: 700, y: 590 },
  G3: { x: 870, y: 590 },
  D3: { x: 990, y: 590 },
  K3: { x: 1110, y: 590 },
  E3: { x: 1230, y: 590 },
  F3: { x: 1440, y: 590 },
  H3e: { x: 2220, y: 590 },
  // ── S3 · y=690 (downtown street) ──────────────
  CS3: { x: 700, y: 690 },
  GS3: { x: 870, y: 690 },
  DS3: { x: 990, y: 690 },
  // ── H4 · y=780 ────────────────────────────────
  H4w: { x: -620, y: 780 },
  A4: { x: 230, y: 780 },
  B4: { x: 530, y: 780 },
  C4: { x: 700, y: 780 },
  G4: { x: 870, y: 780 },
  D4: { x: 990, y: 780 },
  E4: { x: 1230, y: 780 },
  F4: { x: 1440, y: 780 },
  H4e: { x: 2220, y: 780 },
  // ── South freeway · y=920 ─────────────────────
  FWw: { x: -620, y: 920 },
  A5: { x: 230, y: 920 },
  B5: { x: 534, y: 920 },
  C5: { x: 700, y: 920 },
  D5: { x: 990, y: 920 },
  F5: { x: 1440, y: 920 },
  FWe: { x: 2220, y: 920 },
  // ── Vertical road ends (off-canvas) ───────────
  V1n: { x: 230, y: -1100 },
  V1s: { x: 230, y: 2100 },
  Bn: { x: 505, y: -1100 },
  Bs: { x: 558, y: 2100 },
  Cs: { x: 700, y: 2100 },
  Dn: { x: 990, y: -1100 },
  Ds: { x: 990, y: 2100 },
  Fn: { x: 1440, y: -1100 },
  Fs: { x: 1440, y: 2100 },
  // ── North-east arterial crossings ─────────────
  KNE: { x: 1110, y: 535 },
  ENE: { x: 1230, y: 480 },
  FNE: { x: 1440, y: 330 },
  NEe: { x: 2220, y: 200 },
} satisfies Record<string, Point>

export type NodeId = keyof typeof NODES

export type RoadClass = 'freeway' | 'arterial' | 'street'

export interface Road {
  id: string
  class: RoadClass
  nodes: NodeId[]
}

export const ROADS: Road[] = [
  // Freeways
  { id: 'west-freeway', class: 'freeway', nodes: ['Bn', 'B1', 'BS1', 'B2', 'B3', 'B4', 'B5', 'Bs'] },
  { id: 'south-freeway', class: 'freeway', nodes: ['FWw', 'A5', 'B5', 'C5', 'D5', 'F5', 'FWe'] },

  // Arterials
  { id: 'h1', class: 'arterial', nodes: ['H1w', 'A1', 'B1', 'D1', 'K1', 'E1', 'F1', 'H1e'] },
  { id: 'h2', class: 'arterial', nodes: ['H2w', 'A2', 'B2', 'C2', 'X1', 'D2', 'K2', 'E2', 'N2', 'F2', 'H2e'] },
  { id: 'main-street', class: 'arterial', nodes: ['H3w', 'A3', 'B3', 'C3', 'G3', 'D3', 'K3', 'E3', 'F3', 'H3e'] },
  { id: 'h4', class: 'arterial', nodes: ['H4w', 'A4', 'B4', 'C4', 'G4', 'D4', 'E4', 'F4', 'H4e'] },
  { id: 'v1', class: 'arterial', nodes: ['V1n', 'A1', 'AS1', 'A2', 'A3', 'A4', 'A5', 'V1s'] },
  { id: 'v-d', class: 'arterial', nodes: ['Dn', 'D1', 'D2', 'D3', 'DS3', 'D4', 'D5', 'Ds'] },
  { id: 'v-f', class: 'arterial', nodes: ['Fn', 'F1', 'FNE', 'F2', 'F3', 'F4', 'F5', 'Fs'] },
  { id: 'diagonal', class: 'arterial', nodes: ['B1', 'X1', 'D3'] },
  { id: 'north-east', class: 'arterial', nodes: ['D3', 'KNE', 'ENE', 'N2', 'FNE', 'NEe'] },

  // Streets
  { id: 's1', class: 'street', nodes: ['S1w', 'AS1', 'BS1'] },
  { id: 's3', class: 'street', nodes: ['CS3', 'GS3', 'DS3'] },
  { id: 'v-c', class: 'street', nodes: ['C2', 'C3', 'CS3', 'C4', 'C5', 'Cs'] },
  { id: 'v-g', class: 'street', nodes: ['G3', 'GS3', 'G4'] },
  { id: 'v-k', class: 'street', nodes: ['K1', 'K2', 'KNE', 'K3'] },
  { id: 'v-e', class: 'street', nodes: ['E1', 'E2', 'ENE', 'E3', 'E4'] },
]

/**
 * Decorative curved roads. They are drawn like arterials but are not part of the
 * routable graph (markers never use them).
 */
export const DECORATIVE_ROADS: { id: string; class: RoadClass; d: string }[] = [
  // Sweeping parkway from the west edge, across V1, merging into the freeway at B4
  { id: 'parkway', class: 'arterial', d: 'M-620 704 L-20 708 C 70 716, 160 712, 230 672 S 420 770, 530 780' },
  // North-east ramp curving off the top arterial
  { id: 'ne-ramp', class: 'street', d: 'M1230 140 C 1320 104, 1440 70, 1620 52 S 2000 30, 2220 24' },
]

/**
 * Decorative river: a wide band with thin banks, drawn beneath every road (roads cross it
 * on bridges). It runs between H4 and the south freeway, clear of all routes and lots.
 */
export const RIVER_PATH =
  'M-700 880 C -400 884, -200 900, 0 872 S 300 828, 460 856 S 760 902, 980 864 S 1300 818, 1600 848 S 2000 896, 2300 878'

export const pointOf = (id: NodeId): Point => NODES[id]

/** Adjacency set: "A|B" for every pair of consecutive nodes on any road. */
const EDGE_SET = new Set<string>()
for (const road of ROADS) {
  for (let i = 0; i < road.nodes.length - 1; i++) {
    EDGE_SET.add(`${road.nodes[i]}|${road.nodes[i + 1]}`)
    EDGE_SET.add(`${road.nodes[i + 1]}|${road.nodes[i]}`)
  }
}

export const isEdge = (a: NodeId, b: NodeId) => EDGE_SET.has(`${a}|${b}`)

/** Returns a list of problems; empty means the route is a closed walk along real roads. */
export function validateRoute(route: NodeId[]): string[] {
  const errors: string[] = []
  if (route.length < 3) errors.push('route needs at least 3 nodes')
  for (let i = 0; i < route.length; i++) {
    const a = route[i]
    const b = route[(i + 1) % route.length]
    if (!(a in NODES)) errors.push(`unknown node ${a}`)
    if (!isEdge(a, b)) errors.push(`${a} → ${b} is not a street segment`)
  }
  return errors
}

/** Dev check: road segments that cross without a shared node (a route could never turn there). */
export function findUnsharedCrossings(): string[] {
  const segs: { road: string; a: NodeId; b: NodeId }[] = []
  for (const road of ROADS)
    for (let i = 0; i < road.nodes.length - 1; i++)
      segs.push({ road: road.id, a: road.nodes[i], b: road.nodes[i + 1] })

  const problems: string[] = []
  for (let i = 0; i < segs.length; i++) {
    for (let j = i + 1; j < segs.length; j++) {
      const s = segs[i]
      const t = segs[j]
      if (s.road === t.road) continue
      if ([s.a, s.b].some((n) => n === t.a || n === t.b)) continue
      if (segmentsCross(NODES[s.a], NODES[s.b], NODES[t.a], NODES[t.b]))
        problems.push(`${s.road} ${s.a}-${s.b} crosses ${t.road} ${t.a}-${t.b}`)
    }
  }
  return problems
}

function segmentsCross(p1: Point, p2: Point, p3: Point, p4: Point) {
  const d = (a: Point, b: Point, c: Point) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)
  const d1 = d(p3, p4, p1)
  const d2 = d(p3, p4, p2)
  const d3 = d(p1, p2, p3)
  const d4 = d(p1, p2, p4)
  return d1 * d2 < 0 && d3 * d4 < 0
}
