import { memo, useImperativeHandle, useRef, type Ref } from 'react'
import { CityMapLayers } from './map/CityMap'
import { MAP_HEIGHT, MAP_WIDTH } from './map/network'
import { Markers } from './animation/Markers'
import { useCityAnimation, type AnimationController } from './animation/useCityAnimation'
import { useFraming } from './animation/useFraming'

interface Props {
  ref?: Ref<AnimationController | null>
  onPlayStateChange?: (playing: boolean) => void
  className?: string
}

const INITIAL_VIEWBOX = `0 0 ${MAP_WIDTH} ${MAP_HEIGHT}`

/**
 * Decorative animated map that fills its parent. Two stacked SVGs share one viewBox:
 *  - the static street network (thousands of paths, painted once and cached),
 *  - a light layer with only the moving markers, so animating them never repaints the streets.
 * The optional `ref` exposes play / pause / restart / setSpeed for external controls.
 */
export const AnimatedCityMap = memo(function AnimatedCityMap({ ref, onPlayStateChange, className }: Props) {
  const rootRef = useRef<HTMLDivElement>(null)
  useFraming(rootRef) // keeps markers clear of the frame edge at any size
  const controller = useCityAnimation(rootRef, onPlayStateChange)

  useImperativeHandle(
    ref,
    () => ({
      play: () => controller.current?.play(),
      pause: () => controller.current?.pause(),
      restart: () => controller.current?.restart(),
      setSpeed: (s: number) => controller.current?.setSpeed(s),
    }),
    [controller],
  )

  return (
    // `data-ready` is added by useCityAnimation once the animation is running; until then
    // the map is hidden and the CSS-only loader shows (after a short delay). See index.css.
    <div ref={rootRef} className={['city-map-frame', className].filter(Boolean).join(' ')} aria-hidden="true">
      <svg
        className="city-map-layer city-map-static"
        viewBox={INITIAL_VIEWBOX}
        preserveAspectRatio="xMidYMid slice"
        focusable="false"
      >
        <CityMapLayers />
      </svg>
      <svg
        className="city-map-layer city-map-motion"
        viewBox={INITIAL_VIEWBOX}
        preserveAspectRatio="xMidYMid slice"
        focusable="false"
      >
        <Markers />
      </svg>
      <div className="city-map-loader">
        <span />
      </div>
    </div>
  )
})
