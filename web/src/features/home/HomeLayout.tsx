import { Outlet } from 'react-router'
import { SiteFooter } from '../../components/site/SiteFooter'
import { SiteNav } from '../../components/site/SiteNav'
import '../../components/site/site.css'

/** Frame of the public home page; the other pages keep the shared app Layout. */
export function HomeLayout() {
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
