import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { HowItWorksSection } from './HowItWorksSection'

afterEach(() => {
  cleanup()
})

describe('how it works', () => {
  it('lists the three steps in order and marks where login is needed', () => {
    render(<HowItWorksSection />)

    const steps = within(screen.getByRole('list')).getAllByRole('listitem')
    expect(steps.map((step) => within(step).getByRole('heading').textContent)).toEqual([
      'Choose a lot and time slot',
      'Check and pick a space',
      'Log in and confirm',
    ])
    expect(within(steps[2]).getByText('Login required')).toBeDefined()
  })
})
