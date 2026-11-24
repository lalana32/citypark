import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { useState } from 'react'
import { afterEach, describe, expect, it } from 'vitest'
import type { Interval } from './interval'
import { IntervalPicker, type IntervalField } from './IntervalPicker'

afterEach(cleanup)

function Harness({ errors }: { errors?: Partial<Record<IntervalField, string>> }) {
  const [value, setValue] = useState<Interval>({ date: '2026-12-01', start: '07:00', end: '07:30' })
  return <IntervalPicker opensAt="07:00:00" closesAt="22:00:00" value={value} onChange={setValue} errors={errors} />
}

function options(label: string): string[] {
  const select = screen.getByLabelText(label) as HTMLSelectElement
  return Array.from(select.options, (option) => option.value)
}

describe('IntervalPicker', () => {
  it('offers only 15-minute steps inside the opening hours', () => {
    render(<Harness />)

    const starts = options('From')
    expect(starts[0]).toBe('07:00')
    expect(starts.at(-1)).toBe('21:45')
    expect(starts).toHaveLength(60)
    expect(starts.every((time) => /:(00|15|30|45)$/.test(time))).toBe(true)

    const ends = options('To')
    expect(ends[0]).toBe('07:15')
    expect(ends.at(-1)).toBe('22:00')
  })

  it('offers only ends after the chosen start', () => {
    render(<Harness />)

    fireEvent.change(screen.getByLabelText('From'), { target: { value: '12:00' } })

    expect(options('To')[0]).toBe('12:15')
    expect((screen.getByLabelText('To') as HTMLSelectElement).value).toBe('12:15')
  })

  it('keeps the end when it is still after the new start', () => {
    render(<Harness />)

    fireEvent.change(screen.getByLabelText('To'), { target: { value: '18:00' } })
    fireEvent.change(screen.getByLabelText('From'), { target: { value: '09:00' } })

    expect((screen.getByLabelText('To') as HTMLSelectElement).value).toBe('18:00')
  })

  it('shows an error next to its field', () => {
    render(<Harness errors={{ end: 'The interval must be at least 30 minutes long.' }} />)

    const end = screen.getByLabelText('To')
    expect(end.getAttribute('aria-invalid')).toBe('true')
    expect(document.getElementById(end.getAttribute('aria-describedby')!)!.textContent).toBe(
      'The interval must be at least 30 minutes long.',
    )
    expect(screen.getByLabelText('From').getAttribute('aria-invalid')).toBeNull()
  })
})
