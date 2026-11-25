type Props = {
  spaceLabel: string
  onReserve: () => void
}

/** What a guest sees instead of the confirm step: sign-in is asked for at "Reserve". */
export function GuestBooking({ spaceLabel, onReserve }: Props) {
  return (
    <section className="lot-booking" aria-label="Booking">
      <h3 className="lot-booking-title">Book space {spaceLabel}</h3>
      <p className="lot-success-lead">Log in or create an account to confirm. Your time and space stay chosen.</p>
      <div className="lot-booking-actions">
        <button className="site-btn site-btn-primary" type="button" onClick={onReserve}>
          Reserve
        </button>
      </div>
    </section>
  )
}
