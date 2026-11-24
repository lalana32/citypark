import { addMinutes, type Interval, quarterHours, STEP_MINUTES } from './interval'

export type IntervalField = keyof Interval

type Props = {
  opensAt: string
  closesAt: string
  value: Interval
  onChange: (value: Interval) => void
  errors?: Partial<Record<IntervalField, string>>
}

export function IntervalPicker({ opensAt, closesAt, value, onChange, errors = {} }: Props) {
  const starts = quarterHours(opensAt, addMinutes(closesAt, -STEP_MINUTES))
  const ends = quarterHours(addMinutes(value.start, STEP_MINUTES), closesAt)

  function changeStart(start: string) {
    // Keep the end after the start, so the picker never offers an empty interval.
    const end = value.end > start ? value.end : addMinutes(start, STEP_MINUTES)
    onChange({ ...value, start, end })
  }

  return (
    <fieldset>
      <legend>Interval</legend>

      <label htmlFor="interval-date">Date</label>
      <input
        id="interval-date"
        type="date"
        required
        value={value.date}
        onChange={(event) => onChange({ ...value, date: event.target.value })}
        {...errorProps('date', errors)}
      />
      <FieldError field="date" errors={errors} />

      <label htmlFor="interval-start">From</label>
      <select
        id="interval-start"
        value={value.start}
        onChange={(event) => changeStart(event.target.value)}
        {...errorProps('start', errors)}
      >
        {starts.map((time) => (
          <option key={time}>{time}</option>
        ))}
      </select>
      <FieldError field="start" errors={errors} />

      <label htmlFor="interval-end">To</label>
      <select
        id="interval-end"
        value={value.end}
        onChange={(event) => onChange({ ...value, end: event.target.value })}
        {...errorProps('end', errors)}
      >
        {ends.map((time) => (
          <option key={time}>{time}</option>
        ))}
      </select>
      <FieldError field="end" errors={errors} />
    </fieldset>
  )
}

function errorProps(field: IntervalField, errors: Props['errors'] = {}) {
  return errors[field] ? { 'aria-invalid': true, 'aria-describedby': `interval-${field}-error` } : {}
}

function FieldError({ field, errors }: { field: IntervalField; errors: NonNullable<Props['errors']> }) {
  return errors[field] ? <p id={`interval-${field}-error`}>{errors[field]}</p> : null
}
