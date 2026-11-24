import { QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { CENTAR, createTestQueryClient, page, problem, stubApi } from '../../test/apiStub'
import { LotsPage } from './LotsPage'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function renderPage() {
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter>
        <LotsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('LotsPage', () => {
  it('lists each lot with its rate and opening hours', async () => {
    stubApi({ 'GET /api/v1/lots': () => Response.json(page([CENTAR])) })
    renderPage()

    const row = (await screen.findByRole('link', { name: 'Centar' })).closest('tr')!
    expect(within(row).getByText('2.50 BAM / hour')).toBeDefined()
    expect(within(row).getByText('07:00–22:00')).toBeDefined()
    expect(screen.getByRole('link', { name: 'Centar' }).getAttribute('href')).toBe(`/lots/${CENTAR.id}`)
  })

  it('shows a loading state until the lots arrive', async () => {
    stubApi({ 'GET /api/v1/lots': () => Response.json(page([CENTAR])) })
    renderPage()

    expect(screen.getByRole('status').textContent).toBe('Loading lots…')
    expect(await screen.findByText('Centar')).toBeDefined()
    expect(screen.queryByRole('status')).toBeNull()
  })

  it('shows an error when the lots cannot be loaded', async () => {
    stubApi({ 'GET /api/v1/lots': () => problem(500) })
    renderPage()

    expect((await screen.findByRole('alert')).textContent).toBe('We could not load the parking lots. Please try again.')
  })

  it('says so when there are no lots', async () => {
    stubApi({ 'GET /api/v1/lots': () => Response.json(page([])) })
    renderPage()

    expect(await screen.findByText('There are no parking lots yet.')).toBeDefined()
  })

  it('asks for the next page', async () => {
    const fetchMock = stubApi({ 'GET /api/v1/lots': () => Response.json(page([CENTAR], 2)) })
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Next' }))

    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))
    const second = fetchMock.mock.calls[1][0] as Request
    expect(new URL(second.url).searchParams.get('page')).toBe('2')
  })
})
