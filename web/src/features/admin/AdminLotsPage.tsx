import { useState } from 'react'
import { Pager } from '../../components/Pager'
import { formatRate, formatWindow, type Lot, useLots } from '../lots/api'
import { NEW_LOT, toLotValues, useSaveLot } from './api'
import { LotForm } from './LotForm'

// null: no form open; { lot: undefined }: a new lot; { lot }: editing that lot.
type Editing = { lot?: Lot } | null

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
      {editing ? (
        <LotEditor
          key={editing.lot?.id ?? 'new'}
          lot={editing.lot}
          onDone={(lot) => {
            setEditing(null)
            setSaved(`Lot ${lot.name} saved.`)
          }}
          onCancel={() => setEditing(null)}
        />
      ) : (
        <button type="button" onClick={() => open({})}>
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
                    <button type="button" onClick={() => open({ lot })} aria-label={`Edit ${lot.name}`}>
                      Edit
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
