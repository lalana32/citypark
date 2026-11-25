export type PlanSpaceState = 'free' | 'occupied' | 'selected'
export type PlanSpace = { id: string; label: string; state: PlanSpaceState }

export const VIEW_W = 1400
export const HEADER_H = 136
const AISLE_H = 790
const AISLE_GAP = 40
const BOTTOM = 34
export const SPACE_W = 172
export const SPACE_H = 252
const PER_ROW = 6
const PER_AISLE = PER_ROW * 2

export function aisleCount(spaces: number): number {
  return Math.max(1, Math.ceil(spaces / PER_AISLE))
}

export function planHeight(spaces: number): number {
  const aisles = aisleCount(spaces)
  return HEADER_H + aisles * AISLE_H + (aisles - 1) * AISLE_GAP + BOTTOM
}

export function aisleTop(aisle: number): number {
  return HEADER_H + aisle * (AISLE_H + AISLE_GAP)
}

/** Where the n-th space sits: 6 above the lane, 6 below, then the next aisle. */
export function spaceBox(index: number) {
  const inAisle = index % PER_AISLE
  const top = inAisle < PER_ROW
  const x = 114 + 200 * (inAisle % PER_ROW)
  const y = aisleTop(Math.floor(index / PER_AISLE)) + (top ? 48 : 488)
  return { x, y, top }
}

export function availableCount(spaces: readonly PlanSpace[]): number {
  return spaces.filter((space) => space.state !== 'occupied').length
}

export function planSummary(title: string, spaces: readonly PlanSpace[]): string {
  const selected = spaces.find((space) => space.state === 'selected')
  return (
    `Plan of ${title}: ${spaces.length} spaces, ${availableCount(spaces)} available` +
    (selected ? `, space ${selected.label} selected` : '')
  )
}
