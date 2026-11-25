import { Link, useLocation, useNavigate, useSearchParams } from 'react-router'
import { useLogin } from './api'
import { AuthPage } from './AuthPage'
import { CredentialsForm } from './CredentialsForm'
import { readReturnTo, withReturnTo } from './returnTo'

export function LoginPage() {
  const login = useLogin()
  const navigate = useNavigate()
  const location = useLocation()
  const [params] = useSearchParams()
  const returnTo = readReturnTo(params)
  const notice = loginNotice(location.state)

  return (
    <AuthPage
      title="Sign in"
      intro="Sign in to book a space and see your reservations."
      notice={notice}
      footer={
        <>
          No account? <Link to={withReturnTo('/register', returnTo)}>Create one</Link>
        </>
      }
    >
      <CredentialsForm
        submitLabel="Sign in"
        passwordAutoComplete="current-password"
        onSubmit={async (credentials) => {
          await login.mutateAsync(credentials)
          await navigate(returnTo ?? '/', { replace: true })
        }}
      />
    </AuthPage>
  )
}

export const SESSION_ENDED = 'Your session has ended. Sign in again to continue.'

function loginNotice(state: unknown): string | null {
  const flags = state as { registered?: boolean; sessionEnded?: boolean } | null
  if (flags?.registered) {
    return 'Account created. Sign in to continue.'
  }
  return flags?.sessionEnded ? SESSION_ENDED : null
}
