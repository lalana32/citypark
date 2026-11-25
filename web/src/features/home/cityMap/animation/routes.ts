import { roundedPath } from '../map/geometry'
import { CORNER_RADIUS, findUnsharedCrossings, pointOf, validateRoute } from '../map/network'
import { DRIVERS, type DriverConfig } from './config'

export interface DriverRoute extends DriverConfig {
  /** Closed SVG path the marker follows (starts/ends mid-segment for a seamless seam). */
  d: string
}

export const DRIVER_ROUTES: DriverRoute[] = DRIVERS.map((driver) => ({
  ...driver,
  d: roundedPath(driver.route.map(pointOf), CORNER_RADIUS, true),
}))

if (import.meta.env.DEV) {
  for (const driver of DRIVERS) {
    const errors = validateRoute(driver.route)
    if (errors.length) console.error(`[routes] ${driver.id} is invalid:\n  ${errors.join('\n  ')}`)
  }
  const crossings = findUnsharedCrossings()
  if (crossings.length) console.warn(`[network] roads cross without a shared node:\n  ${crossings.join('\n  ')}`)
}
