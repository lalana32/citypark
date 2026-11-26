import { useEffect } from 'react'
import { Outlet, useLocation } from 'react-router'
import { SiteFooter } from './SiteFooter'
import { SiteNav } from './SiteNav'
import './site.css'

/** Shared frame of every page: navigation, page content and footer. */
export function SiteLayout() {
  const { pathname } = useLocation()
  useScrollOnNavigate()

  return (
    <div className="site">
      <SiteNav overHero={pathname === '/'} />
      <main>
        <Outlet />
      </main>
      <SiteFooter />
    </div>
  )
}

// The router does not scroll on client-side navigation: a #section link scrolls to that section,
// a new page opens at the top. Query changes on the same page (e.g. the lot interval) keep the position.
function useScrollOnNavigate() {
  const { pathname, hash, key } = useLocation()

  useEffect(() => {
    if (!hash) {
      window.scrollTo({ top: 0, left: 0, behavior: 'instant' })
    }
  }, [pathname, hash])

  useEffect(() => {
    if (hash) {
      document.getElementById(decodeURIComponent(hash.slice(1)))?.scrollIntoView({ behavior: scrollBehavior() })
    }
  }, [hash, key])
}

function scrollBehavior(): ScrollBehavior {
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth'
}
