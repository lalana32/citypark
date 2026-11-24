import { Navigate, Outlet, useLocation } from 'react-router'
import { useSession } from './api'

export function RequireAuth() {
  const session = useSession()
  const location = useLocation()

  if (session.isPending) {
    return <p role="status">Loading…</p>
  }

  if (session.isError) {
    return <p role="alert">We could not check your session. Please refresh the page.</p>
  }

  if (!session.data) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  return <Outlet />
}
