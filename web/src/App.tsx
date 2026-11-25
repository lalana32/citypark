import { Navigate, Route, Routes } from 'react-router'
import { SiteLayout } from './components/site/SiteLayout'
import { AdminLayout } from './features/admin/AdminLayout'
import { AdminLotsPage } from './features/admin/AdminLotsPage'
import { AdminReservationsPage } from './features/admin/AdminReservationsPage'
import { LoginPage } from './features/auth/LoginPage'
import { RegisterPage } from './features/auth/RegisterPage'
import { RequireAuth } from './features/auth/RequireAuth'
import { RequireRole } from './features/auth/RequireRole'
import { HomePage } from './features/home/HomePage'
import { LotAvailabilityPage } from './features/lots/LotAvailabilityPage'
import { LotsPage } from './features/lots/LotsPage'
import { MyReservationsPage } from './features/reservations/MyReservationsPage'

const ADMIN = 'Admin'

function App() {
  return (
    <Routes>
      <Route element={<SiteLayout />}>
        <Route index element={<HomePage />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/lots" element={<LotsPage />} />
        <Route path="/lots/:id" element={<LotAvailabilityPage />} />
        <Route element={<RequireAuth />}>
          <Route path="/reservations" element={<MyReservationsPage />} />
          <Route element={<RequireRole role={ADMIN} />}>
            <Route element={<AdminLayout />}>
              <Route path="/admin" element={<Navigate to="/admin/lots" replace />} />
              <Route path="/admin/lots" element={<AdminLotsPage />} />
              <Route path="/admin/reservations" element={<AdminReservationsPage />} />
            </Route>
          </Route>
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}

export default App
