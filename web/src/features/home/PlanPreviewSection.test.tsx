import { cleanup, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it } from 'vitest'
import { PlanPreviewSection } from './PlanPreviewSection'

afterEach(cleanup)

function renderSection() {
  render(
    <MemoryRouter>
      <PlanPreviewSection />
    </MemoryRouter>,
  )
}

describe('plan preview', () => {
  it('shows an example plan with one space selected', () => {
    renderSection()

    expect(
      screen.getByRole('img', { name: 'Plan of Parking 01 · Level A: 12 spaces, 7 available, space A-04 selected' }),
    ).toBeDefined()
    const panel = screen.getByRole('complementary', { name: 'Selected space' })
    expect(within(panel).getByText('A-04')).toBeDefined()
    expect(within(panel).getByText('Estimated price')).toBeDefined()
  })

  it('cannot be used to pick or reserve a space', () => {
    renderSection()

    expect(screen.queryByRole('button')).toBeNull()
    expect(screen.getByRole('link', { name: 'Find a parking space' }).getAttribute('href')).toBe('/lots')
  })
})
