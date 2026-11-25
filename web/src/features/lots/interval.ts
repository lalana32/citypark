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

const lotDate = new Intl.DateTimeFormat('en-GB', {
  timeZone: LOT_TIME_ZONE,
  day: 'numeric',
  month: 'short',
  year: 'numeric',
})
const lotTime = new Intl.DateTimeFormat('en-GB', {
  timeZone: LOT_TIME_ZONE,
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
})

/** Any ISO instant -> "1 Dec 2026, 10:00" as the clock on the lot shows it. */
export function formatLotDateTime(instant: string): string {
  const date = new Date(instant)
  return `${lotDate.format(date)}, ${lotTime.format(date)}`
}

/** Any ISO instant -> "1 Dec 2026" on the lot calendar. */
export function formatLotDay(instant: string): string {
  return lotDate.format(new Date(instant))
}

/** Two ISO instants -> "10:00–11:30" on the lot clock. */
export function formatLotTimes(start: string, end: string): string {
  return `${lotTime.format(new Date(start))}–${lotTime.format(new Date(end))}`
}

/** Today's date on the lot, as "YYYY-MM-DD". */
export function lotToday(now = new Date()): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: LOT_TIME_ZONE }).format(now)
}

const DAY_MINUTES = 24 * 60

/** From the next quarter hour on the lot clock, for `minutes`; null if it would run into the next day. */
export function nextSlot(now = new Date(), minutes = 120): Interval | null {
  const start = Math.ceil(toMinutes(lotTime.format(now)) / STEP_MINUTES) * STEP_MINUTES
  const end = start + minutes
  if (end >= DAY_MINUTES) {
    return null
  }
  return { date: lotToday(now), start: toTime(start), end: toTime(end) }
}

/** True when the interval lies inside the lot's opening hours; it may end at closing time. */
export function isOpenFor(lot: { opensAt: string; closesAt: string }, interval: Interval): boolean {
  return (
    toMinutes(lot.opensAt) <= toMinutes(interval.start) && toMinutes(interval.end) <= toMinutes(lot.closesAt)
  )
}

/** Reads ?date=&start=&end= back; null unless it is a real day and a slot the picker offers. */
export function readInterval(params: URLSearchParams, lot: { opensAt: string; closesAt: string }): Interval | null {
  const date = params.get('date') ?? ''
  const start = params.get('start') ?? ''
  const end = params.get('end') ?? ''
  const starts = quarterHours(lot.opensAt, addMinutes(lot.closesAt, -STEP_MINUTES))
  if (!isDate(date) || !starts.includes(start) || !quarterHours(addMinutes(start, STEP_MINUTES), lot.closesAt).includes(end)) {
    return null
  }
  return { date, start, end }
}

// Round-trips through UTC, so "2026-02-31" is rejected.
function isDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return false
  }
  const [year, month, day] = value.split('-').map(Number)
  return new Date(Date.UTC(year, month - 1, day)).toISOString().slice(0, 10) === value
}

/** "10:00" to "11:15" -> "1 h 15 min". */
export function formatDuration(start: string, end: string): string {
  const total = toMinutes(end) - toMinutes(start)
  const hours = Math.floor(total / 60)
  const minutes = total % 60
  return [hours > 0 && `${hours} h`, minutes > 0 && `${minutes} min`].filter(Boolean).join(' ')
}

const plainDate = new Intl.DateTimeFormat('en-GB', { timeZone: 'UTC', day: 'numeric', month: 'short', year: 'numeric' })

/** "2026-12-01" -> "1 Dec 2026"; the date is already a lot date, so no zone shift. */
export function formatDate(date: string): string {
  const [year, month, day] = date.split('-').map(Number)
  return plainDate.format(new Date(Date.UTC(year, month - 1, day)))
}
