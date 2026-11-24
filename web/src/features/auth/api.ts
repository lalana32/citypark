import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../api/client'
import type { components } from '../../api/schema'
import { type FormErrors, toAuthFormErrors } from './authErrors'

export type CurrentUser = components['schemas']['CurrentUserResponse']
export type Credentials = { email: string; password: string }
export type CredentialField = keyof Credentials

const CREDENTIAL_FIELDS: readonly CredentialField[] = ['email', 'password']

export const sessionKey = ['session'] as const

export function hasRole(user: CurrentUser | null | undefined, role: string) {
  return user?.roles.includes(role) ?? false
}

export class AuthFormError extends Error {
  readonly errors: FormErrors<CredentialField>

  constructor(errors: FormErrors<CredentialField>) {
    super(errors.form ?? 'The form has invalid fields.')
    this.name = 'AuthFormError'
    this.errors = errors
  }
}

// null means "signed out", which is a normal answer, not an error.
async function fetchSession(): Promise<CurrentUser | null> {
  const { data, response } = await api.GET('/api/v1/auth/me')
  if (response.status === 401) {
    return null
  }
  if (!data) {
    throw new Error(`The session request failed with ${response.status}.`)
  }
  return data
}

export function useSession() {
  return useQuery({
    queryKey: sessionKey,
    queryFn: fetchSession,
    staleTime: 5 * 60 * 1000,
    retry: false,
  })
}

export function useLogin() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (body: Credentials) => {
      const { data, error, response } = await api.POST('/api/v1/auth/login', { body })
      if (!data) {
        throw new AuthFormError(toAuthFormErrors(response, error, CREDENTIAL_FIELDS))
      }
      return data
    },
    onSuccess: (user) => queryClient.setQueryData(sessionKey, user),
  })
}

export function useRegister() {
  return useMutation({
    mutationFn: async (body: Credentials) => {
      const { data, error, response } = await api.POST('/api/v1/auth/register', { body })
      if (!data) {
        throw new AuthFormError(toAuthFormErrors(response, error, CREDENTIAL_FIELDS))
      }
      return data
    },
  })
}

export function useLogout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async () => {
      const { response } = await api.POST('/api/v1/auth/logout')
      // 401 means the session was already gone, which is the outcome we want.
      if (!response.ok && response.status !== 401) {
        throw new Error(`The logout request failed with ${response.status}.`)
      }
    },
    onSuccess: () => {
      // Drop the old user's data. The session query stays so the mounted header sees the change.
      queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== sessionKey[0] })
      queryClient.setQueryData(sessionKey, null)
    },
  })
}
