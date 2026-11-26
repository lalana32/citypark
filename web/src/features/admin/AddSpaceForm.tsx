import { useForm } from 'react-hook-form'
import type { Lot } from '../lots/api'
import { UNEXPECTED_ERROR } from '../reservations/api'
import { type Space, SpaceLabelError, useAddSpace } from './api'

type Props = { lot: Lot; onAdded: (space: Space) => void; onClose: () => void }

// Stays open after a save, so an admin can add several spaces in a row.
export function AddSpaceForm({ lot, onAdded, onClose }: Props) {
  const addSpace = useAddSpace(lot.id)
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<{ label: string }>({ defaultValues: { label: '' } })

  const submit = handleSubmit(async ({ label }) => {
    try {
      onAdded(await addSpace.mutateAsync(label))
      reset()
    } catch (error) {
      setError('label', { message: error instanceof SpaceLabelError ? error.message : UNEXPECTED_ERROR })
    }
  })

  return (
    <div className="site-card admin-panel">
      <h3 className="site-display admin-panel-title">Add a space to {lot.name}</h3>
      <form onSubmit={submit} noValidate className="admin-form admin-form-single">
        <div className="admin-field">
          <label htmlFor="space-label">Label</label>
          <input
            id="space-label"
            className="admin-input"
            aria-invalid={errors.label ? true : undefined}
            aria-describedby={errors.label ? 'space-label-error' : undefined}
            {...register('label', { required: 'Label is required.' })}
          />
          {errors.label && (
            <p id="space-label-error" className="admin-field-error">
              {errors.label.message}
            </p>
          )}
        </div>
        <div className="admin-form-actions">
          <button type="submit" className="site-btn site-btn-primary" disabled={isSubmitting}>
            Add space
          </button>
          <button type="button" className="site-btn site-btn-secondary" onClick={onClose}>
            Done
          </button>
        </div>
      </form>
    </div>
  )
}
