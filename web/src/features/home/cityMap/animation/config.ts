import type { NodeId } from '../map/network'

/**
 * ANIMATION CONFIGURATION
 * -----------------------
 * Everything you are likely to tweak lives here.
 */

/** Length of one complete, seamless loop in seconds (at 1× speed). */
// 16 s = the original 5.6 s loop played at 0.35× (5.6 / 0.35).
export const CYCLE_DURATION = 16

/** Initial playback speed multiplier (the control panel can change it live). */
export const DEFAULT_SPEED = 1

/**
 * Minimum gap, in screen pixels, between the frame edge and anything that moves
 * (the outer glow of a marker, a parking badge). The map is framed so this always holds.
 */
export const MOTION_PADDING_PX = 12

/** Outer (glow) radius of a marker in map units — must match Markers.tsx; used for framing. */
export const MARKER_RADIUS = 28

/**
 * How far (map units) the map is shifted right on wide screens, so the diagonal arterial
 * stays clear of the hero headline. The framing never shifts so far that a marker would
 * touch the right edge, so the effective shift can be a little smaller.
 */
export const HERO_MAP_SHIFT = 150

/** Below this frame width the hero stacks text above the map (matches index.css). */
export const HERO_STACK_BREAKPOINT = 900

/**
 * Trail behind each marker, built from stacked dashes on the route path.
 * Each entry is [length in map units, opacity]; longer = fainter, giving a soft fade.
 */
export const TRAIL_LAYERS: [number, number][] = [
  [70, 0.32],
  [150, 0.16],
  [260, 0.07],
]

/** How close (map units) a driver must pass a lot to "check" it. */
export const PARKING_CHECK_DISTANCE = 48

/** Duration of the "lot is full" pulse, seconds. */
export const PARKING_PULSE_DURATION = 0.9

export interface DriverConfig {
  id: string
  /** Trail colour (the marker itself is styled in map/theme.ts). */
  color: string
  /**
   * Closed loop of node names (see src/map/network.ts). The last node connects back to
   * the first, and each consecutive pair must be a street segment.
   */
  route: NodeId[]
  /**
   * Where on its loop the driver is at t = 0, as a fraction 0–1.
   * Offsets stagger the drivers without breaking the loop (no delays involved).
   */
  offset: number
}

export const DRIVERS: DriverConfig[] = [
  {
    // Downtown loop: freeway → H2 → diagonal → S3 → back west on main street.
    id: 'driver-a',
    color: '#8DB1FF',
    route: ['B2', 'C2', 'X1', 'D3', 'DS3', 'GS3', 'CS3', 'C3', 'B3'],
    offset: 0,
  },
  {
    // East loop: curved NE arterial → H2 → V-F → H4 → V-E → back west on main street.
    id: 'driver-b',
    color: '#8DB1FF',
    route: ['D3', 'KNE', 'ENE', 'N2', 'F2', 'F3', 'F4', 'E4', 'E3', 'K3'],
    offset: 0,
  },
  {
    // West loop: V1 south → H4 → up the west freeway → S1 → back down V1.
    id: 'driver-c',
    color: '#8DB1FF',
    route: ['A2', 'A3', 'A4', 'B4', 'B3', 'B2', 'BS1', 'AS1'],
    offset: 0.34,
  },
]
