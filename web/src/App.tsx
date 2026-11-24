import { Link, Navigate, Outlet, Route, Routes } from 'react-router'
import { LoginPage } from './features/auth/LoginPage'
import { RegisterPage } from './features/auth/RegisterPage'
import { RequireAuth } from './features/auth/RequireAuth'
import { UserMenu } from './features/auth/UserMenu'
import { LotAvailabilityPage } from './features/lots/LotAvailabilityPage'
import { LotsPage } from './features/lots/LotsPage'

function Layout() {
  return (
    <>
      <header>
        <h1>CityPark</h1>
        <nav>
          <Link to="/lots">Parking lots</Link>
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
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}

export default App
