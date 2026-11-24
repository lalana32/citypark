import { Link, useLocation, useNavigate } from 'react-router'
import { useLogin } from './api'
import { CredentialsForm } from './CredentialsForm'

export function LoginPage() {
  const login = useLogin()
  const navigate = useNavigate()
  const location = useLocation()

  return (
    <section>
      <h2>Sign in</h2>
      <CredentialsForm
        submitLabel="Sign in"
        passwordAutoComplete="current-password"
        onSubmit={async (credentials) => {
          await login.mutateAsync(credentials)
          await navigate(returnPath(location.state), { replace: true })
        }}
      />
      <p>
        No account? <Link to="/register">Create one</Link>
      </p>
    </section>
  )
}

// RequireAuth stores where the user was heading; only accept an in-app path.
function returnPath(state: unknown): string {
  const from = (state as { from?: { pathname?: unknown } } | null)?.from?.pathname
  return typeof from === 'string' && from.startsWith('/') && !from.startsWith('//') ? from : '/'
}
