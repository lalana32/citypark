import type { Availability } from './api'

export type FreeSpace = Availability['freeSpaces'][number]

export const NO_FREE_SPACES = 'No free spaces for this time'

type Props = {
  availability: Availability
  selectedId: string | null
  onSelect: (space: FreeSpace) => void
}

export function SpaceGrid({ availability, selectedId, onSelect }: Props) {
  if (availability.freeSpaces.length === 0) {
    return (
      <section className="lot-spaces lot-empty" aria-label="Free spaces">
        <p className="lot-empty-title">{NO_FREE_SPACES}</p>
        <p className="lot-empty-hint">Try an earlier or later time, or another date.</p>
      </section>
    )
  }

  return (
    <section className="lot-spaces" aria-label="Free spaces">
      <p className="lot-spaces-count">
        {availability.availableSpaces} of {availability.totalSpaces} spaces free
      </p>
      <ul className="lot-grid">
        {availability.freeSpaces.map((space) => (
          <li key={space.id}>
            <button
              type="button"
              className="lot-space"
              aria-label={`Space ${space.label}`}
              aria-pressed={space.id === selectedId}
              onClick={() => onSelect(space)}
            >
              {space.label}
            </button>
          </li>
        ))}
      </ul>
    </section>
  )
}
