import { memo } from 'react'
import { TRAIL_LAYERS } from './config'
import { DRIVER_ROUTES } from './routes'
import { THEME } from '../map/theme'

/**
 * Route paths (invisible guides + fading trails) and the driver markers.
 * Markers are drawn at the origin; GSAP translates them along their route.
 */
export const Markers = memo(function Markers() {
  const { core, halo } = THEME.marker
  return (
    <g className="drivers">
      {DRIVER_ROUTES.map((route) => (
        <g key={route.id} data-driver={route.id}>
          <path className="route-guide" d={route.d} fill="none" stroke="none" />
          {TRAIL_LAYERS.map(([, opacity], i) => (
            <path
              key={i}
              className="trail"
              d={route.d}
              fill="none"
              stroke={route.color}
              strokeOpacity={opacity}
              strokeWidth={3.4}
              strokeLinecap="round"
              strokeLinejoin="round"
              // Hidden until GSAP sets the dash pattern (avoids a flash of the full route).
              visibility="hidden"
            />
          ))}
          <g className="marker" visibility="hidden">
            {/* Outer radius must match MARKER_RADIUS in config.ts (used for framing). */}
            <circle r="28" fill={halo} fillOpacity="0.1" />
            <circle r="16" fill={halo} fillOpacity="0.24" />
            <circle r="6.5" fill={core} />
          </g>
        </g>
      ))}
    </g>
  )
})
