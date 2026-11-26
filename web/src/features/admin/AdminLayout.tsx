import { NavLink, Outlet } from 'react-router'
import './admin.css'

export function AdminLayout() {
  return (
    <div className="admin-page">
      <div className="site-wrap">
        <nav aria-label="Admin" className="admin-nav">
          <NavLink to="/admin/lots">Lots and spaces</NavLink>
          <NavLink to="/admin/reservations">All reservations</NavLink>
        </nav>
        <Outlet />
      </div>
    </div>
  )
}
