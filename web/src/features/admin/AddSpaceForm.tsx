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
    <>
      <h3>Add a space to {lot.name}</h3>
      <form onSubmit={submit} noValidate>
        <label htmlFor="space-label">Label</label>
        <input
          id="space-label"
          aria-invalid={errors.label ? true : undefined}
          aria-describedby={errors.label ? 'space-label-error' : undefined}
          {...register('label', { required: 'Label is required.' })}
        />
        {errors.label && <p id="space-label-error">{errors.label.message}</p>}
        <button type="submit" disabled={isSubmitting}>
          Add space
        </button>
        <button type="button" onClick={onClose}>
          Done
        </button>
      </form>
    </>
  )
}
