import { Link, useNavigate } from 'react-router'
import { useRegister } from './api'
import { CredentialsForm } from './CredentialsForm'

export function RegisterPage() {
  const registerAccount = useRegister()
  const navigate = useNavigate()

  return (
    <section>
      <h2>Create an account</h2>
      <CredentialsForm
        submitLabel="Create account"
        passwordAutoComplete="new-password"
        onSubmit={async (credentials) => {
          await registerAccount.mutateAsync(credentials)
          // The API does not sign in after registering.
          await navigate('/login', { state: { registered: true } })
        }}
      />
      <p>
        Already have an account? <Link to="/login">Sign in</Link>
      </p>
    </section>
  )
}
