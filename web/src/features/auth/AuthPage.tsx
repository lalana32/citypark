import type { ReactNode } from 'react'
import './auth.css'

type Props = {
  title: string
  intro: string
  notice?: string | null
  children: ReactNode
  /** The line under the form that links to the other page. */
  footer: ReactNode
}

/** The shared frame of the login and registration pages. */
export function AuthPage({ title, intro, notice, children, footer }: Props) {
  return (
    <section className="auth-page">
      <div className="site-wrap">
        <div className="site-card auth-card">
          <p className="site-eyebrow">CityPark account</p>
          <h1 className="site-display auth-heading">{title}</h1>
          <p className="auth-intro">{intro}</p>
          {notice && (
            <p role="status" className="auth-notice">
              {notice}
            </p>
          )}
          {children}
          <p className="auth-switch">{footer}</p>
        </div>
      </div>
    </section>
  )
}
