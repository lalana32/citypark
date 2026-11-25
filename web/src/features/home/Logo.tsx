/** 44 × 28 "P" sign used as the CityPark logo mark. */
export function Logo({ tone = 'dark' }: { tone?: 'dark' | 'light' }) {
  const plate = tone === 'dark' ? { fill: '#141c3a', stroke: '#3a4a9a' } : { fill: '#0b1024', stroke: '#0b1024' }

  return (
    <svg width="44" height="28" viewBox="-22 -14 44 28" aria-hidden="true">
      <rect x="-21.5" y="-13.5" width="43" height="27" rx="5" fill={plate.fill} stroke={plate.stroke} />
      <path
        d="M-5 8V-8H0.5a4.5 4.5 0 0 1 0 9H-5"
        fill="none"
        stroke="#8db1ff"
        strokeWidth="2.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}
