import { type DemoLot, freeSpaces, SPACE_NUMBERS, spaceLabel } from './demoLots'

const VIEW_W = 1400
const VIEW_H = 960
const SPACE_W = 172
const SPACE_H = 252

type Props = {
  lot: DemoLot
  selected: string | null
  onSelect: (space: string) => void
}

function spaceBox(index: number) {
  const top = index < 6
  return { x: 114 + 200 * (index % 6), y: top ? 184 : 624, top }
}

/** 12-space plan: the SVG draws the lot, real buttons on top of it make the spaces selectable. */
export function ParkingPlan({ lot, selected, onSelect }: Props) {
  const free = freeSpaces(lot)

  return (
    <div className="plan">
      <svg
        viewBox={`0 0 ${VIEW_W} ${VIEW_H}`}
        role="img"
        aria-label={`Plan of ${lot.name}, level ${lot.level}: ${SPACE_NUMBERS.length} spaces, ${free} available`}
      >
        <PlanDefs />
        <rect width={VIEW_W} height={VIEW_H} fill="#ffffff" />

        <g transform="translate(82 74)">
          <rect x="-22" y="-14" width="44" height="28" rx="5" fill="#0b1024" />
          <path d="M-5 8V-8H0.5a4.5 4.5 0 0 1 0 9H-5" fill="none" stroke="#8db1ff" strokeWidth="2.6" strokeLinecap="round" strokeLinejoin="round" />
        </g>
        <text className="plan-title" x="122" y="70" fontSize="30" fill="#0b1024">
          {lot.name} · Level {lot.level}
        </text>
        <text x="122" y="98" fontSize="16" fill="#5b6788">
          {free} of {SPACE_NUMBERS.length} spaces available
        </text>
        <Legend />

        <rect x="60" y="136" width="1280" height="790" rx="28" fill="#f3f5fa" stroke="#e4e9f2" strokeWidth="1.5" />
        <Markings />

        {SPACE_NUMBERS.map((space, index) => {
          const { x, y, top } = spaceBox(index)
          const cx = x + SPACE_W / 2
          const occupied = lot.occupied.includes(space)
          const labelY = top ? y + 28 : y + 240

          if (occupied) {
            return (
              <g key={space}>
                <text x={cx} y={labelY} textAnchor="middle" fontSize="17" fontWeight="600" fill="#5b6788">
                  {spaceLabel(lot, space)}
                </text>
                <g filter="url(#plan-soft-shadow)" transform={`translate(${cx} ${top ? 334 : 726})${top ? '' : ' rotate(180)'}`}>
                  <use href="#plan-car" />
                </g>
              </g>
            )
          }

          const iconY = top ? 314 : 726
          return (
            <g key={space}>
              <rect
                x={x}
                y={y}
                width={SPACE_W}
                height={SPACE_H}
                rx="16"
                fill={space === selected ? '#eef3ff' : '#ffffff'}
                stroke="#e1e6f0"
                strokeWidth="1.5"
                filter="url(#plan-card-shadow)"
              />
              <text x={cx} y={labelY} textAnchor="middle" fontSize="17" fontWeight="600" fill="#0b1024">
                {spaceLabel(lot, space)}
              </text>
              <path
                transform={`translate(${cx} ${iconY}) scale(2.3)`}
                d="M-5 8V-8H0.5a4.5 4.5 0 0 1 0 9H-5"
                fill="none"
                stroke="#7f9bea"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <text x={cx} y={iconY + 56} textAnchor="middle" fontSize="15" fill="#5b6788">
                Available
              </text>
            </g>
          )
        })}
      </svg>

      {SPACE_NUMBERS.map((space, index) => {
        const { x, y } = spaceBox(index)
        const occupied = lot.occupied.includes(space)
        const isSelected = space === selected
        const state = occupied ? 'occupied' : isSelected ? 'selected' : 'available'

        return (
          <button
            key={space}
            type="button"
            className="plan-space"
            style={{
              left: `${(x / VIEW_W) * 100}%`,
              top: `${(y / VIEW_H) * 100}%`,
              width: `${(SPACE_W / VIEW_W) * 100}%`,
              height: `${(SPACE_H / VIEW_H) * 100}%`,
            }}
            aria-label={`Space ${spaceLabel(lot, space)}, ${state}`}
            aria-pressed={occupied ? undefined : isSelected}
            disabled={occupied}
            onClick={() => onSelect(space)}
          >
            {isSelected && (
              <span className="plan-check" aria-hidden="true">
                <svg viewBox="0 0 24 24" fill="none">
                  <path d="M6 12.5l4 4 8-9" stroke="#ffffff" strokeWidth="2.6" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </span>
            )}
          </button>
        )
      })}
    </div>
  )
}

function Legend() {
  const items = [
    { label: 'Available', fill: '#ffffff', stroke: '#d3dae8' },
    { label: 'Selected', fill: '#eef3ff', stroke: '#3e63d8' },
    { label: 'Occupied', fill: '#2e43a3', stroke: '#2e43a3' },
  ]

  return (
    <g>
      {items.map((item, index) => (
        <g key={item.label} transform={`translate(${1000 + index * 110} 84)`}>
          <rect x="0" y="-9" width="18" height="18" rx="5" fill={item.fill} stroke={item.stroke} strokeWidth="1.5" />
          <text x="28" y="5.5" fontSize="15" fill="#5b6788">
            {item.label}
          </text>
        </g>
      ))}
    </g>
  )
}

function Markings() {
  const stalls = [100, 300, 500, 700, 900, 1100, 1300]

  return (
    <g fill="none" strokeLinecap="round" strokeLinejoin="round">
      <g stroke="#d3dae8" strokeWidth="3">
        <path d="M100 170H1300M100 890H1300" />
        {stalls.map((x) => (
          <path key={x} d={`M${x} 170V450M${x} 610V890`} />
        ))}
        <path d="M140 530H1260" strokeDasharray="18 16" strokeWidth="2.5" />
      </g>
      <path d="M386 478l14 12l-14 12M414 558l-14 12l14 12M986 478l14 12l-14 12M1014 558l-14 12l14 12" stroke="#b4bfd6" strokeWidth="3" />
      <g fontSize="13" fontWeight="600" fill="#5b6788" letterSpacing="0.08em">
        <text x="78" y="494">IN</text>
        <text x="1292" y="594" textAnchor="end">
          OUT
        </text>
      </g>
      <path d="M80 510h26m-8 -7l8 7l-8 7M1320 570h-26m8 -7l-8 7l8 7" stroke="#5b6788" strokeWidth="2.5" />
    </g>
  )
}

function PlanDefs() {
  return (
    <defs>
      <linearGradient id="plan-car-body" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stopColor="#22307a" />
        <stop offset="0.32" stopColor="#3a51b8" />
        <stop offset="0.68" stopColor="#3349aa" />
        <stop offset="1" stopColor="#1e2a6c" />
      </linearGradient>
      <linearGradient id="plan-car-roof" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stopColor="#5a73da" />
        <stop offset="1" stopColor="#3c53b6" />
      </linearGradient>
      <linearGradient id="plan-car-glass" x1="0" y1="0" x2="1" y2="0">
        <stop offset="0" stopColor="#141c3a" />
        <stop offset="1" stopColor="#070b19" />
      </linearGradient>
      <filter id="plan-soft-shadow" x="-30%" y="-30%" width="160%" height="160%">
        <feDropShadow dx="0" dy="6" stdDeviation="8" floodColor="#0b1024" floodOpacity="0.16" />
      </filter>
      <filter id="plan-card-shadow" x="-20%" y="-20%" width="140%" height="140%">
        <feDropShadow dx="0" dy="3" stdDeviation="5" floodColor="#0b1024" floodOpacity="0.07" />
      </filter>
      <g id="plan-car">
        <g transform="rotate(-90) scale(0.94)">
          <path d="M-106,-30 Q-108,-41 -92,-42 L52,-42 C84,-42 104,-32 108,-14 Q110,0 108,14 C104,32 84,42 52,42 L-92,42 Q-108,41 -106,30 Q-109,0 -106,-30 Z" fill="url(#plan-car-body)" stroke="#1b2763" strokeWidth="1.5" />
          <path d="M46,-37 C82,-37 100,-29 104,-14 L104,14 C100,29 82,37 46,37 Z" fill="#8db1ff" opacity="0.28" />
          <path d="M50,-26 C76,-26 92,-20 98,-10 M50,26 C76,26 92,20 98,10" fill="none" stroke="#c9d8ff" strokeWidth="1.6" strokeLinecap="round" opacity="0.6" />
          <rect x="-101" y="-33" width="26" height="66" rx="12" fill="#8db1ff" opacity="0.22" />
          <rect x="20" y="-52" width="22" height="12" rx="6" fill="#2c3f96" stroke="#1b2763" strokeWidth="1.2" />
          <rect x="20" y="40" width="22" height="12" rx="6" fill="#2c3f96" stroke="#1b2763" strokeWidth="1.2" />
          <path d="M-67,-35 L-6,-37 L24,-37 C37,-33 44,-22 45,-14 L45,14 C44,22 37,33 24,37 L-6,37 L-67,35 Q-79,0 -67,-35 Z" fill="url(#plan-car-glass)" />
          <path d="M30,-30 C38,-24 41,-16 41,-12 L41,12 C41,16 38,24 30,30 L24,30 C30,22 31,14 31,0 C31,-14 30,-22 24,-30 Z" fill="#8db1ff" opacity="0.35" />
          <path d="M-62,-26 L-66,-12 L-66,12 L-62,26 L-58,26 L-61,12 L-61,-12 L-58,-26 Z" fill="#8db1ff" opacity="0.25" />
          <rect x="-50" y="-29" width="60" height="58" rx="17" fill="url(#plan-car-roof)" stroke="#1b2763" strokeWidth="1" />
          <path d="M-40,-22 H0 Q4,-22 4,-18 V-14 H-44 V-18 Q-44,-22 -40,-22 Z" fill="#ffffff" opacity="0.18" />
          <path d="M-107,-31 Q-109,-28 -108,-20 L-108,-12 Q-103,-13 -101,-18 L-100,-30 Z" fill="#ff7a9a" />
          <path d="M-107,31 Q-109,28 -108,20 L-108,12 Q-103,13 -101,18 L-100,30 Z" fill="#ff7a9a" />
          <path d="M96,-30 Q104,-27 107,-18 L103,-14 Q98,-18 94,-22 Z" fill="#f4f7ff" stroke="#9fb4e8" strokeWidth="0.8" />
          <path d="M96,30 Q104,27 107,18 L103,14 Q98,18 94,22 Z" fill="#f4f7ff" stroke="#9fb4e8" strokeWidth="0.8" />
        </g>
      </g>
    </defs>
  )
}
