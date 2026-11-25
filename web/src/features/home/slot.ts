export type Slot = { arrival: Date; departure: Date }

const HOUR = 60 * 60 * 1000

/** Default time slot while the date and time search is not on the page: next full hour, for two hours. */
export function defaultSlot(now: Date, hours = 2): Slot {
  const arrival = new Date(now)
  arrival.setMinutes(0, 0, 0)
  arrival.setHours(arrival.getHours() + 1)
  return { arrival, departure: new Date(arrival.getTime() + hours * HOUR) }
}

/** Started hours are billed in full. */
export function billedHours({ arrival, departure }: Slot) {
  return Math.max(1, Math.ceil((departure.getTime() - arrival.getTime()) / HOUR))
}

export function formatSlotTime(date: Date) {
  const day = date.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })
  const time = date.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })
  return `${day} · ${time}`
}
