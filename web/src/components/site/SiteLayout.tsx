import { Outlet } from 'react-router'
import { SiteFooter } from './SiteFooter'
import { SiteNav } from './SiteNav'
import './site.css'

/** Shared frame of every page: navigation, page content and footer. */
export function SiteLayout() {
  return (
    <div className="site">
      <SiteNav />
      <main>
        <Outlet />
      </main>
      <SiteFooter />
    </div>
  )
}
