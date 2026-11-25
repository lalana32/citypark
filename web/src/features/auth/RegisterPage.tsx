import { Link, useNavigate, useSearchParams } from 'react-router'
import { useRegister } from './api'
import { CredentialsForm } from './CredentialsForm'
import { readReturnTo, withReturnTo } from './returnTo'

export function RegisterPage() {
  const registerAccount = useRegister()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const returnTo = readReturnTo(params)

  return (
    <section>
      <h2>Create an account</h2>
      <CredentialsForm
        submitLabel="Create account"
        passwordAutoComplete="new-password"
        onSubmit={async (credentials) => {
          await registerAccount.mutateAsync(credentials)
          // The API does not sign in after registering, so login carries the return target on.
          await navigate(withReturnTo('/login', returnTo), { state: { registered: true } })
        }}
      />
      <p>
        Already have an account? <Link to={withReturnTo('/login', returnTo)}>Sign in</Link>
      </p>
    </section>
  )
}
