import { Link, Outlet } from 'react-router'
import { hasRole, useSession } from './api'

// Sits inside RequireAuth, so the session is already loaded and signed in here.
// The API still enforces the role; this only keeps other users off the pages.
export function RequireRole({ role }: { role: string }) {
  const session = useSession()

  if (!hasRole(session.data, role)) {
    return (
      <section>
        <h2>Access denied</h2>
        <p role="alert">You do not have access to this page.</p>
        <Link to="/lots">Back to parking lots</Link>
      </section>
    )
  }

  return <Outlet />
}
