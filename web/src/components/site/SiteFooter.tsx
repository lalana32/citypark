import { Link } from 'react-router'
import { Logo } from './Logo'
import './siteFooter.css'

// Placeholders until the real contact details are known (CP-039 open questions).
const CONTACT = ['[EMAIL]', '[PHONE]', '[ADDRESS]']

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
                <Link to="/#locations">Parking lots</Link>
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
              {CONTACT.map((line) => (
                <li key={line}>{line}</li>
              ))}
            </ul>
          </div>

          <div>
            <h2 className="footer-heading">Legal</h2>
            <ul className="footer-list">
              <li>
                <Link to="/terms">Terms of use</Link>
              </li>
              <li>
                <Link to="/privacy">Privacy policy</Link>
              </li>
            </ul>
          </div>
        </div>

        <p className="footer-bottom">© {new Date().getFullYear()} CityPark. All rights reserved.</p>
      </div>
    </footer>
  )
}
