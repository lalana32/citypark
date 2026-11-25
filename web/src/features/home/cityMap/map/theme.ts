/** Visual tokens for the map (hero-background palette). */
export const THEME = {
  background: '#06080F',
  backgroundDeep: '#03040A',
  glow: '#2436C8',

  /** Local streets: thin, broken, irregular spacing (see districts.ts). */
  localStreet: '#1F2862',
  localStreetWidth: 0.9,
  building: '#0C1230',
  areaTint: '#2436C8',

  /** Four line levels, heaviest last so it is drawn on top. */
  road: {
    street: { color: '#2D3A7E', width: 1.5 }, // collector streets (sabirne)
    arterial: { color: '#3F4FA6', width: 3 }, // main streets (glavne ulice)
    freeway: { color: '#5A6FD6', width: 4.6 }, // arterial highways (magistrale)
  },

  river: {
    bank: '#3B4A98',
    water: '#070A16',
    width: 46,
    bankWidth: 1.5,
  },

  parking: {
    stroke: '#6279E0', // occupied: outline only
    fill: '#0B1024',
    free: '#8DB1FF', // free: bright, with a soft glow
    full: '#ff7a9a', // flash when a driver checks a full lot
  },

  marker: {
    core: '#FFFFFF',
    halo: '#8DB1FF',
  },
} as const
