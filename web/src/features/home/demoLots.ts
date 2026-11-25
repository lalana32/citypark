// Static demo data until the home page is wired to /lots (see CP-039 open questions).

export type DemoLot = {
  id: string
  name: string
  address: string
  pricePerHour: number
  level: string
  occupied: readonly string[]
}

export const SPACE_NUMBERS = ['01', '02', '03', '04', '05', '06', '07', '08', '09', '10', '11', '12'] as const

export const DEMO_LOTS: readonly DemoLot[] = [
  {
    id: 'parking-01',
    name: 'Parking 01',
    address: '[Street and number], [City]',
    pricePerHour: 2,
    level: 'A',
    occupied: ['02', '05', '07', '10', '11'],
  },
  {
    id: 'parking-02',
    name: 'Parking 02',
    address: '[Street and number], [City]',
    pricePerHour: 1.5,
    level: 'A',
    occupied: ['01', '04', '09'],
  },
  {
    id: 'parking-03',
    name: 'Parking 03',
    address: '[Street and number], [City]',
    pricePerHour: 2.5,
    level: 'A',
    occupied: ['01', '03', '04', '06', '08', '09', '10', '12'],
  },
]

export const totalSpaces = () => SPACE_NUMBERS.length

export const freeSpaces = (lot: DemoLot) => SPACE_NUMBERS.length - lot.occupied.length

export const spaceLabel = (lot: DemoLot, space: string) => `${lot.level}-${space}`

export const formatKm = (amount: number) => `${amount.toFixed(2)} KM`
