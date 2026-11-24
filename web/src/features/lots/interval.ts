export const STEP_MINUTES = 15

export const LOT_TIME_ZONE = 'Europe/Sarajevo'

// A day and two "HH:mm" times, read as local lot time.
export type Interval = { date: string; start: string; end: string }

/** Every quarter hour from `from` (rounded up to the grid) to `to`, both "HH:mm" or "HH:mm:ss". */
export function quarterHours(from: string, to: string): string[] {
  const first = Math.ceil(toMinutes(from) / STEP_MINUTES) * STEP_MINUTES
  const last = toMinutes(to)
  const slots: string[] = []
  for (let minutes = first; minutes <= last; minutes += STEP_MINUTES) {
    slots.push(toTime(minutes))
  }
  return slots
}

/** "2026-12-01" + "10:00" -> "2026-12-01T10:00:00+01:00", using the lot's offset on that day. */
export function toLotInstant(date: string, time: string): string {
  return `${date}T${time}:00${lotOffset(date, time)}`
}

export function addMinutes(time: string, minutes: number): string {
  return toTime(toMinutes(time) + minutes)
}

function lotOffset(date: string, time: string): string {
  const [year, month, day] = date.split('-').map(Number)
  const [hours, minutes] = time.split(':').map(Number)
  const instant = new Date(Date.UTC(year, month - 1, day, hours, minutes))
  const name = new Intl.DateTimeFormat('en-US', { timeZone: LOT_TIME_ZONE, timeZoneName: 'longOffset' })
    .formatToParts(instant)
    .find((part) => part.type === 'timeZoneName')?.value
  // "GMT+01:00", or plain "GMT" for a zero offset.
  return name && name !== 'GMT' ? name.slice(3) : '+00:00'
}

function toMinutes(time: string): number {
  const [hours, minutes] = time.split(':').map(Number)
  return hours * 60 + minutes
}

function toTime(minutes: number): string {
  return `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`
}
