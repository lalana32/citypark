import './howItWorks.css'

const STEPS = [
  {
    title: 'Choose a lot and time slot',
    text: 'Pick a location and the date and time you arrive and leave.',
    needsAccount: false,
  },
  {
    title: 'Check and pick a space',
    text: 'See free and occupied spaces for your time slot and pick exactly the one you want.',
    needsAccount: false,
  },
  {
    title: 'Log in and confirm',
    text: 'You are asked to log in only at the end, when you complete the reservation.',
    needsAccount: true,
  },
]

export function HowItWorksSection() {
  return (
    <section id="how-it-works" className="site-sec" aria-labelledby="how-it-works-title">
      <div className="site-wrap">
        <p className="site-eyebrow">How it works</p>
        <h2 id="how-it-works-title" className="site-h2 site-display">
          Three steps to a reservation
        </h2>

        <ol className="steps">
          {STEPS.map((step, index) => (
            <li key={step.title} className="step">
              <span className="step-mark site-display" aria-hidden="true">
                {index + 1}
              </span>
              <h3 className="step-title site-display">{step.title}</h3>
              <p className="step-text">{step.text}</p>
              <span className={step.needsAccount ? 'step-tag step-tag-account' : 'step-tag'}>
                {step.needsAccount ? 'Login required' : 'No account'}
              </span>
            </li>
          ))}
        </ol>
      </div>
    </section>
  )
}
