import { memo, useMemo } from 'react'
import { roundedPath } from './geometry'
import { DISTRICTS, districtStreets, polyPoints } from './districts'
import { CORNER_RADIUS, DECORATIVE_ROADS, RIVER_PATH, ROADS, pointOf, type RoadClass } from './network'
import { PARKING_LOTS, type ParkingLot } from './parking'
import { THEME } from './theme'

const ROAD_ORDER: RoadClass[] = ['street', 'arterial', 'freeway']

/** Static map layers (rendered once; never re-rendered during animation). */
export const CityMapLayers = memo(function CityMapLayers() {
  const districts = useMemo(
    () => DISTRICTS.map((d) => ({ district: d, d: districtStreets(d), points: polyPoints(d) })),
    [],
  )

  const roadsByClass = useMemo(() => {
    const out: Record<RoadClass, string[]> = { street: [], arterial: [], freeway: [] }
    for (const road of ROADS) out[road.class].push(roundedPath(road.nodes.map(pointOf), CORNER_RADIUS, false))
    for (const road of DECORATIVE_ROADS) out[road.class].push(road.d)
    return out
  }, [])

  const { river } = THEME

  return (
    <>
      <defs>
        <radialGradient id="glow" cx="50%" cy="50%" r="50%">
          <stop offset="0%" stopColor={THEME.glow} stopOpacity="0.5" />
          <stop offset="55%" stopColor={THEME.glow} stopOpacity="0.12" />
          <stop offset="100%" stopColor={THEME.glow} stopOpacity="0" />
        </radialGradient>
        <radialGradient id="vignette" cx="50%" cy="50%" r="72%">
          <stop offset="50%" stopColor={THEME.backgroundDeep} stopOpacity="0" />
          <stop offset="100%" stopColor={THEME.backgroundDeep} stopOpacity="0.9" />
        </radialGradient>
        <radialGradient id="lot-glow" cx="50%" cy="50%" r="50%">
          <stop offset="0%" stopColor={THEME.parking.free} stopOpacity="0.35" />
          <stop offset="100%" stopColor={THEME.parking.free} stopOpacity="0" />
        </radialGradient>
        {districts.map(({ district, points }) => (
          <clipPath key={district.id} id={`clip-${district.id}`}>
            <polygon points={points} />
          </clipPath>
        ))}
      </defs>

      <rect x="-3000" y="-3000" width="7600" height="7000" fill={THEME.background} />

      {/* Two soft blue light pools for depth */}
      <g className="glows" pointerEvents="none">
        <ellipse cx="1120" cy="700" rx="460" ry="300" fill="url(#glow)" />
        <ellipse cx="1420" cy="360" rx="300" ry="240" fill="url(#glow)" opacity="0.6" />
      </g>

      <g className="area-tints">
        {districts
          .filter(({ district }) => district.tint)
          .map(({ district, points }) => (
            <polygon key={district.id} points={points} fill={THEME.areaTint} opacity={district.tint} />
          ))}
      </g>

      {/* Local streets (thin, broken, irregular) and building footprints */}
      <g
        className="local-streets"
        fill="none"
        stroke={THEME.localStreet}
        strokeWidth={THEME.localStreetWidth}
        strokeLinecap="round"
      >
        {districts.map(({ district, d }) =>
          d && district.kind !== 'blocks' ? (
            <path key={district.id} d={d} clipPath={`url(#clip-${district.id})`} />
          ) : null,
        )}
      </g>
      <g className="buildings" fill={THEME.building} stroke={THEME.localStreet} strokeWidth="0.6">
        {districts.map(({ district, d }) =>
          d && district.kind === 'blocks' ? (
            <path key={district.id} d={d} clipPath={`url(#clip-${district.id})`} />
          ) : null,
        )}
      </g>

      {/* River: banks + dark water, beneath every road */}
      <g className="river" fill="none" strokeLinecap="round" strokeLinejoin="round">
        <path d={RIVER_PATH} stroke={river.bank} strokeWidth={river.width} />
        <path d={RIVER_PATH} stroke={river.water} strokeWidth={river.width - river.bankWidth * 2} />
      </g>

      {/* Road network, lightest level first so the arterial highways sit on top */}
      <g className="roads" fill="none" strokeLinecap="round" strokeLinejoin="round">
        {ROAD_ORDER.map((cls) => (
          <path
            key={cls}
            d={roadsByClass[cls].join('')}
            stroke={THEME.road[cls].color}
            strokeWidth={THEME.road[cls].width}
          />
        ))}
      </g>

      {/* Vignette follows the visible window (the framing hook updates .viewport-fill). */}
      <rect className="viewport-fill" x="0" y="0" width="1600" height="1000" fill="url(#vignette)" pointerEvents="none" />

      <g className="parking">
        {PARKING_LOTS.map((lot) => (
          <ParkingBadge key={lot.id} lot={lot} />
        ))}
      </g>
    </>
  )
})

/**
 * Monoline "P" drawn in the map's own line language (round caps and joins, like the roads).
 * 16 units tall, optically centred in the 44 × 28 lot; the bowl spans just over half the height.
 */
const PARKING_GLYPH = 'M-5 8V-8H0.5a4.5 4.5 0 0 1 0 9H-5'

/** 44 × 28 lot marked with a "P", on a dark pad that masks the streets beneath it. */
function ParkingBadge({ lot }: { lot: ParkingLot }) {
  const p = THEME.parking
  const color = lot.free ? p.free : p.stroke
  const transform = `translate(${lot.x} ${lot.y})${lot.rotate ? ` rotate(${lot.rotate})` : ''}`
  return (
    <g data-lot={lot.id} transform={transform}>
      {lot.free && <ellipse rx="44" ry="34" fill="url(#lot-glow)" />}
      <rect x="-28" y="-20" width="56" height="40" rx="8" fill={THEME.background} />
      {/* Pulse ring, animated when a driver checks this (full) lot */}
      <circle className="lot-ring" r="24" fill="none" stroke={p.full} strokeWidth="1.4" opacity="0" />
      <rect
        className="lot-badge"
        x="-22"
        y="-14"
        width="44"
        height="28"
        rx="3"
        fill={p.fill}
        stroke={color}
        strokeWidth="1.5"
      />
      {/* Counter-rotated so the letter stays upright in lots that follow a vertical or diagonal street. */}
      <path
        className="lot-glyph"
        d={PARKING_GLYPH}
        transform={lot.rotate ? `rotate(${-lot.rotate})` : undefined}
        stroke={color}
        strokeWidth="2.6"
        strokeLinecap="round"
        strokeLinejoin="round"
        fill="none"
      />
    </g>
  )
}
