import { useEffect } from 'react'
import { Outlet, useLocation } from 'react-router'
import { SiteFooter } from './SiteFooter'
import { SiteNav } from './SiteNav'
import './site.css'

/** Shared frame of every page: navigation, page content and footer. */
export function SiteLayout() {
  const { pathname } = useLocation()
  useScrollToHash()

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

// The router does not scroll to #section on client-side navigation, so the layout does it.
function useScrollToHash() {
  const { hash, key } = useLocation()

  useEffect(() => {
    if (hash) {
      document.getElementById(decodeURIComponent(hash.slice(1)))?.scrollIntoView()
    }
  }, [hash, key])
}
