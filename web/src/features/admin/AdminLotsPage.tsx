import { useState } from 'react'
import { Pager } from '../../components/Pager'
import { formatRate, formatWindow, type Lot, useLots } from '../lots/api'
import { AddSpaceForm } from './AddSpaceForm'
import { NEW_LOT, toLotValues, useSaveLot } from './api'
import { LotForm } from './LotForm'

// A lot form (no lot means a new one), the add-space form for a lot, or nothing open.
type Editing = { kind: 'lot'; lot?: Lot } | { kind: 'space'; lot: Lot } | null

export function AdminLotsPage() {
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<Editing>(null)
  const [saved, setSaved] = useState<string | null>(null)
  const lots = useLots(page)

  const open = (next: Editing) => {
    setSaved(null)
    setEditing(next)
  }

  return (
    <section>
      <h2>Manage parking lots</h2>
      {saved && <p role="status">{saved}</p>}
      {editing?.kind === 'lot' && (
        <LotEditor
          key={editing.lot?.id ?? 'new'}
          lot={editing.lot}
          onDone={(lot) => {
            setEditing(null)
            setSaved(`Lot ${lot.name} saved.`)
          }}
          onCancel={() => setEditing(null)}
        />
      )}
      {editing?.kind === 'space' && (
        <AddSpaceForm
          key={editing.lot.id}
          lot={editing.lot}
          onAdded={(space) => setSaved(`Space ${space.label} added to ${editing.lot.name}.`)}
          onClose={() => setEditing(null)}
        />
      )}
      {!editing && (
        <button type="button" onClick={() => open({ kind: 'lot' })}>
          New lot
        </button>
      )}

      {lots.isPending && <p role="status">Loading lots…</p>}
      {lots.isError && <p role="alert">We could not load the parking lots. Please try again.</p>}
      {lots.data && lots.data.items.length === 0 && <p>There are no parking lots yet.</p>}
      {lots.data && lots.data.items.length > 0 && (
        <>
          <table>
            <thead>
              <tr>
                <th scope="col">Lot</th>
                <th scope="col">Address</th>
                <th scope="col">Rate</th>
                <th scope="col">Open</th>
                <th scope="col">Spaces</th>
                <th scope="col">Actions</th>
              </tr>
            </thead>
            <tbody>
              {lots.data.items.map((lot) => (
                <tr key={lot.id}>
                  <td>{lot.name}</td>
                  <td>{lot.address}</td>
                  <td>{formatRate(lot)}</td>
                  <td>{formatWindow(lot)}</td>
                  <td>{lot.totalSpaces}</td>
                  <td>
                    <button type="button" onClick={() => open({ kind: 'lot', lot })} aria-label={`Edit ${lot.name}`}>
                      Edit
                    </button>
                    <button
                      type="button"
                      onClick={() => open({ kind: 'space', lot })}
                      aria-label={`Add a space to ${lot.name}`}
                    >
                      Add space
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <Pager page={page} totalPages={Number(lots.data.totalPages ?? 1)} onChange={setPage} />
        </>
      )}
    </section>
  )
}

type EditorProps = { lot: Lot | undefined; onDone: (lot: Lot) => void; onCancel: () => void }

function LotEditor({ lot, onDone, onCancel }: EditorProps) {
  const save = useSaveLot(lot?.id)

  return (
    <>
      <h3>{lot ? `Edit ${lot.name}` : 'New lot'}</h3>
      <LotForm
        defaultValues={lot ? toLotValues(lot) : NEW_LOT}
        submitLabel={lot ? 'Save changes' : 'Create lot'}
        onSubmit={async (values) => onDone(await save.mutateAsync(values))}
        onCancel={onCancel}
      />
    </>
  )
}
