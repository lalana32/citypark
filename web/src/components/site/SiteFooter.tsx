import { Link } from 'react-router'
import { Logo } from './Logo'
import './siteFooter.css'

// CityPark is a demo, so the address uses the reserved .example domain (RFC 2606).
const CONTACT_EMAIL = 'contact@citypark.example'

export function SiteFooter() {
  return (
    <footer id="contact" className="site-footer site-dark">
      <div className="site-wrap">
        <div className="footer-cols">
          <div>
            <Link to="/" className="site-logo" aria-label="CityPark home">
              <Logo />
              <span className="site-display">CityPark</span>
            </Link>
            <p className="footer-about">Parking space reservations across the city.</p>
          </div>

          <nav aria-label="Footer">
            <h2 className="footer-heading">Navigation</h2>
            <ul className="footer-list">
              <li>
                <Link to="/">Home</Link>
              </li>
              <li>
                <Link to="/lots">Parking lots</Link>
              </li>
              <li>
                <Link to="/#how-it-works">How it works</Link>
              </li>
              <li>
                <Link to="/#faq">FAQ</Link>
              </li>
            </ul>
          </nav>

          <div>
            <h2 className="footer-heading">Contact</h2>
            <ul className="footer-list">
              <li>
                <a href={`mailto:${CONTACT_EMAIL}`}>{CONTACT_EMAIL}</a>
              </li>
            </ul>
          </div>
        </div>

        <p className="footer-bottom">© {new Date().getFullYear()} CityPark · A demo project.</p>
      </div>
    </footer>
  )
}
