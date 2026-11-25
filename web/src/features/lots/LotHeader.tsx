import { Link } from 'react-router'
import { formatRate, formatWindow, type Lot } from './api'

export const BACK_TO_LOTS = '/lots'

export function LotHeader({ lot }: { lot: Lot }) {
  return (
    <header className="lot-header">
      <Link className="lot-back" to={BACK_TO_LOTS}>
        ← Back to parking lots
      </Link>
      <h1 className="site-h2 site-display">{lot.name}</h1>
      <p className="lot-address">{lot.address}</p>
      <dl className="lot-facts">
        <div>
          <dt>Price</dt>
          <dd>{formatRate(lot)}</dd>
        </div>
        <div>
          <dt>Open</dt>
          <dd>{formatWindow(lot)}</dd>
        </div>
      </dl>
    </header>
  )
}
