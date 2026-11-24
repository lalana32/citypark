import { useNavigate } from 'react-router'
import { useLogout, useSession } from './api'

export function UserMenu() {
  const session = useSession()
  const logout = useLogout()
  const navigate = useNavigate()

  if (!session.data) {
    return null
  }

  return (
    <nav aria-label="Account">
      <span>{session.data.email}</span>
      <button
        type="button"
        disabled={logout.isPending}
        onClick={() => logout.mutate(undefined, { onSuccess: () => navigate('/login', { replace: true }) })}
      >
        Log out
      </button>
      {logout.isError && <p role="alert">Logging out failed. Please try again.</p>}
    </nav>
  )
}
