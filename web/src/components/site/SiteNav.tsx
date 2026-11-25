import { useEffect, useRef, useState } from 'react'
import { Link, NavLink, useNavigate } from 'react-router'
import { hasRole, useLogout, useSession } from '../../features/auth/api'
import { Logo } from './Logo'
import './siteNav.css'

const ADMIN = 'Admin'

type NavItem = { label: string; href: string; route?: boolean }

const GUEST_LINKS: NavItem[] = [
  { label: 'Home', href: '/', route: true },
  { label: 'Parking lots', href: '/#locations' },
  { label: 'How it works', href: '/#how-it-works' },
  { label: 'Contact', href: '/#contact' },
]

const USER_LINKS: NavItem[] = [
  { label: 'Home', href: '/', route: true },
  { label: 'Parking lots', href: '/#locations' },
  { label: 'My reservations', href: '/reservations', route: true },
]

const ADMIN_LINK: NavItem = { label: 'Admin', href: '/admin/lots', route: true }

/** Top navigation of every page; section links go to the home page sections. */
export function SiteNav() {
  const session = useSession()
  const [menuOpen, setMenuOpen] = useState(false)

  const user = session.data
  const links = user ? [...USER_LINKS, ...(hasRole(user, ADMIN) ? [ADMIN_LINK] : [])] : GUEST_LINKS
  const closeMenu = () => setMenuOpen(false)

  return (
    <header className="site-header site-dark">
      <div className="site-wrap site-header-bar">
        <Link to="/" className="site-logo" aria-label="CityPark home">
          <Logo />
          <span className="site-display">CityPark</span>
        </Link>

        <nav className="site-nav-desktop" aria-label="Main">
          <NavLinks links={links} />
        </nav>

        <div className="site-nav-desktop site-header-actions">
          {user ? (
            <AccountMenu />
          ) : (
            <>
              <Link className="site-navlink site-login" to="/login">
                Log in
              </Link>
              <Link className="site-btn site-btn-primary site-btn-small" to="/register">
                Register
              </Link>
            </>
          )}
        </div>

        <div className="site-nav-mobile site-header-actions">
          {!user && (
            <Link className="site-btn site-btn-primary site-btn-small" to="/register">
              Register
            </Link>
          )}
          <button
            type="button"
            className="site-icon-btn"
            aria-label="Menu"
            aria-expanded={menuOpen}
            aria-controls="site-mobile-menu"
            onClick={() => setMenuOpen((open) => !open)}
          >
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M4 7h16M4 12h16M4 17h16" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            </svg>
          </button>
        </div>
      </div>

      {menuOpen && (
        <nav id="site-mobile-menu" className="site-wrap site-nav-mobile site-mobile-menu" aria-label="Main mobile">
          <NavLinks links={links} onNavigate={closeMenu} />
          {user ? (
            <>
              <Link className="site-navlink" to="/account" onClick={closeMenu}>
                My account
              </Link>
              <LogoutButton className="site-navlink" />
            </>
          ) : (
            <Link className="site-navlink" to="/login" onClick={closeMenu}>
              Log in
            </Link>
          )}
        </nav>
      )}
    </header>
  )
}

function NavLinks({ links, onNavigate }: { links: NavItem[]; onNavigate?: () => void }) {
  return links.map((item) =>
    item.route ? (
      <NavLink key={item.label} className="site-navlink" to={item.href} end onClick={onNavigate}>
        {item.label}
      </NavLink>
    ) : (
      <Link key={item.label} className="site-navlink" to={item.href} onClick={onNavigate}>
        {item.label}
      </Link>
    ),
  )
}

function AccountMenu() {
  const [open, setOpen] = useState(false)
  const root = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) {
      return
    }
    const onPointer = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) {
        setOpen(false)
      }
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false)
      }
    }
    document.addEventListener('pointerdown', onPointer)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('pointerdown', onPointer)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  return (
    <div className="site-account" ref={root}>
      <button
        type="button"
        className="site-account-btn"
        aria-expanded={open}
        aria-controls="site-account-menu"
        onClick={() => setOpen((value) => !value)}
      >
        <span className="site-avatar" aria-hidden="true">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
            <circle cx="12" cy="8" r="4" />
            <path d="M4 21c1-4 4-6 8-6s7 2 8 6" />
          </svg>
        </span>
        My account
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <path d="M6 9l6 6 6-6" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </button>

      {open && (
        <div id="site-account-menu" className="site-account-menu">
          <Link to="/account">Account overview</Link>
          <Link to="/reservations">My reservations</Link>
          <Link to="/profile">Profile</Link>
          <LogoutButton className="site-account-logout" />
        </div>
      )}
    </div>
  )
}

function LogoutButton({ className }: { className: string }) {
  const logout = useLogout()
  const navigate = useNavigate()

  return (
    <>
      <button
        type="button"
        className={className}
        disabled={logout.isPending}
        onClick={() => logout.mutate(undefined, { onSuccess: () => navigate('/login', { replace: true }) })}
      >
        Log out
      </button>
      {logout.isError && <p role="alert">Logging out failed. Please try again.</p>}
    </>
  )
}
