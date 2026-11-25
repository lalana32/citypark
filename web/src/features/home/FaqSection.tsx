import { useState } from 'react'
import './faq.css'

const QUESTIONS = [
  {
    question: 'Can I check availability without registering?',
    answer:
      'Yes. Browsing the locations, checking availability and picking a space need no account. You only log in when you confirm the reservation.',
  },
  {
    question: 'How do I reserve a parking space?',
    answer:
      'Choose a parking lot and time slot, check availability, pick a space on the plan, then log in and confirm the reservation.',
  },
  {
    question: 'Can I cancel a reservation?',
    answer: 'Yes, you manage your reservations under My reservations. [CANCELLATION POLICY: deadline and any fee]',
  },
  {
    question: 'How is the parking price calculated?',
    answer:
      'The price depends on the location and the length of the reservation, and you see an estimate before you confirm. [PRICING DETAILS]',
  },
]

/** Accordion: each question is a button that shows or hides its answer; several can be open at once. */
export function FaqSection() {
  const [open, setOpen] = useState<ReadonlySet<number>>(() => new Set([0]))

  const toggle = (index: number) =>
    setOpen((current) => {
      const next = new Set(current)
      if (!next.delete(index)) {
        next.add(index)
      }
      return next
    })

  return (
    <section id="faq" className="site-sec" aria-labelledby="faq-title">
      <div className="site-wrap faq">
        <div>
          <p className="site-eyebrow">Questions</p>
          <h2 id="faq-title" className="site-h2 site-display">
            Frequently asked questions
          </h2>
        </div>

        <div className="faq-list">
          {QUESTIONS.map((item, index) => {
            const expanded = open.has(index)
            return (
              <div key={item.question} className="faq-item">
                <h3 className="faq-heading">
                  <button
                    type="button"
                    id={`faq-q-${index}`}
                    className="faq-question"
                    aria-expanded={expanded}
                    aria-controls={`faq-a-${index}`}
                    onClick={() => toggle(index)}
                  >
                    {item.question}
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                      <path d="M6 9l6 6 6-6" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  </button>
                </h3>
                <div id={`faq-a-${index}`} role="region" aria-labelledby={`faq-q-${index}`} hidden={!expanded}>
                  <p className="faq-answer">{item.answer}</p>
                </div>
              </div>
            )
          })}
        </div>
      </div>
    </section>
  )
}
