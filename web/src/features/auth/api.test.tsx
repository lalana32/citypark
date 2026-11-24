import { QueryClientProvider } from '@tanstack/react-query'
import { act, cleanup, renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ANA, createTestQueryClient, csrfRoute as csrf, problem, stubApi } from '../../test/apiStub'
import { AuthFormError, sessionKey, useLogin, useLogout, useSession } from './api'
import { INVALID_CREDENTIALS } from './authErrors'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function setup() {
  const queryClient = createTestQueryClient()
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  )
  return { queryClient, wrapper }
}

describe('useSession', () => {
  it('returns the user the cookie belongs to', async () => {
    stubApi({ 'GET /api/v1/auth/me': () => Response.json(ANA) })
    const { wrapper } = setup()

    const { result } = renderHook(() => useSession(), { wrapper })

    await waitFor(() => expect(result.current.data).toEqual(ANA))
  })

  it('returns null, not an error, when nobody is signed in', async () => {
    stubApi({ 'GET /api/v1/auth/me': () => problem(401) })
    const { wrapper } = setup()

    const { result } = renderHook(() => useSession(), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toBeNull()
  })
})

describe('useLogin', () => {
  it('stores the signed-in user as the session', async () => {
    stubApi({ ...csrf, 'POST /api/v1/auth/login': () => Response.json(ANA) })
    const { queryClient, wrapper } = setup()
    const { result } = renderHook(() => useLogin(), { wrapper })

    await act(() => result.current.mutateAsync({ email: ANA.email, password: 'Secret123!' }))

    expect(queryClient.getQueryData(sessionKey)).toEqual(ANA)
  })

  it('fails with the generic message on a wrong password', async () => {
    stubApi({ ...csrf, 'POST /api/v1/auth/login': () => problem(401, { detail: 'The email or password is incorrect.' }) })
    const { queryClient, wrapper } = setup()
    const { result } = renderHook(() => useLogin(), { wrapper })

    const error = await act(() =>
      result.current.mutateAsync({ email: ANA.email, password: 'wrong' }).catch((e: unknown) => e),
    )

    expect(error).toBeInstanceOf(AuthFormError)
    expect((error as AuthFormError).errors.form).toBe(INVALID_CREDENTIALS)
    expect(queryClient.getQueryData(sessionKey)).toBeUndefined()
  })
})

describe('useLogout', () => {
  it('signs out and drops the old user\'s cached data', async () => {
    stubApi({ ...csrf, 'POST /api/v1/auth/logout': () => new Response(null, { status: 204 }) })
    const { queryClient, wrapper } = setup()
    queryClient.setQueryData(sessionKey, ANA)
    queryClient.setQueryData(['reservations'], [{ id: 'r1' }])
    const { result } = renderHook(() => useLogout(), { wrapper })

    await act(() => result.current.mutateAsync())

    expect(queryClient.getQueryData(sessionKey)).toBeNull()
    expect(queryClient.getQueryData(['reservations'])).toBeUndefined()
  })
})
