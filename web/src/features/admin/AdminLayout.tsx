import { NavLink, Outlet } from 'react-router'

export function AdminLayout() {
  return (
    <>
      <nav aria-label="Admin">
        <NavLink to="/admin/lots">Lots and spaces</NavLink>
        <NavLink to="/admin/reservations">All reservations</NavLink>
      </nav>
      <Outlet />
    </>
  )
}
