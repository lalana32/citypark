import { Link, Navigate, Outlet, Route, Routes } from 'react-router'
import { AdminLotsPage } from './features/admin/AdminLotsPage'
import { hasRole, useSession } from './features/auth/api'
import { LoginPage } from './features/auth/LoginPage'
import { RegisterPage } from './features/auth/RegisterPage'
import { RequireAuth } from './features/auth/RequireAuth'
import { RequireRole } from './features/auth/RequireRole'
import { UserMenu } from './features/auth/UserMenu'
import { LotAvailabilityPage } from './features/lots/LotAvailabilityPage'
import { LotsPage } from './features/lots/LotsPage'
import { MyReservationsPage } from './features/reservations/MyReservationsPage'

const ADMIN = 'Admin'

function Layout() {
  const session = useSession()

  return (
    <>
      <header>
        <h1>CityPark</h1>
        <nav>
          <Link to="/lots">Parking lots</Link>
          <Link to="/reservations">My reservations</Link>
          {hasRole(session.data, ADMIN) && <Link to="/admin/lots">Admin</Link>}
        </nav>
        <UserMenu />
      </header>
      <main>
        <Outlet />
      </main>
    </>
  )
}

function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/lots" element={<LotsPage />} />
        <Route path="/lots/:id" element={<LotAvailabilityPage />} />
        <Route element={<RequireAuth />}>
          <Route index element={<p>Parking reservations.</p>} />
          <Route path="/reservations" element={<MyReservationsPage />} />
          <Route element={<RequireRole role={ADMIN} />}>
            <Route path="/admin" element={<Navigate to="/admin/lots" replace />} />
            <Route path="/admin/lots" element={<AdminLotsPage />} />
          </Route>
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}

export default App
