import { type DefaultValues, type FieldErrors, type UseFormRegisterReturn, useForm } from 'react-hook-form'
import { UNEXPECTED_ERROR } from '../reservations/api'
import { type LotField, LotFormError, type LotValues } from './api'

type Props = {
  defaultValues: DefaultValues<LotValues>
  submitLabel: string
  onSubmit: (values: LotValues) => Promise<unknown>
  onCancel: () => void
}

export function LotForm({ defaultValues, submitLabel, onSubmit, onCancel }: Props) {
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LotValues>({ defaultValues })

  const submit = handleSubmit(async (values) => {
    try {
      await onSubmit(values)
    } catch (error) {
      if (!(error instanceof LotFormError)) {
        setError('root.server', { message: UNEXPECTED_ERROR })
        return
      }
      for (const [field, message] of Object.entries(error.errors.fields)) {
        setError(field as LotField, { message })
      }
      if (error.errors.form) {
        setError('root.server', { message: error.errors.form })
      }
    }
  })

  const required = (label: string) => ({ required: `${label} is required.` })

  return (
    <form onSubmit={submit} noValidate>
      {errors.root?.server && <p role="alert">{errors.root.server.message}</p>}
      <Field name="name" label="Name" errors={errors} input={register('name', required('Name'))} />
      <Field name="address" label="Address" errors={errors} input={register('address', required('Address'))} />
      <Field
        name="hourlyPrice"
        label="Hourly price"
        type="number"
        step="0.01"
        errors={errors}
        input={register('hourlyPrice', { ...required('Hourly price'), valueAsNumber: true })}
      />
      <Field name="currency" label="Currency" errors={errors} input={register('currency', required('Currency'))} />
      <Field name="opensAt" label="Opens at" type="time" errors={errors} input={register('opensAt', required('Opens at'))} />
      <Field
        name="closesAt"
        label="Closes at"
        type="time"
        errors={errors}
        input={register('closesAt', required('Closes at'))}
      />
      <button type="submit" disabled={isSubmitting}>
        {submitLabel}
      </button>
      <button type="button" onClick={onCancel}>
        Cancel
      </button>
    </form>
  )
}

type FieldProps = {
  name: LotField
  label: string
  type?: string
  step?: string
  errors: FieldErrors<LotValues>
  input: UseFormRegisterReturn
}

function Field({ name, label, type = 'text', step, errors, input }: FieldProps) {
  const error = errors[name]
  const id = `lot-${name}`
  return (
    <>
      <label htmlFor={id}>{label}</label>
      <input
        id={id}
        type={type}
        step={step}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : undefined}
        {...input}
      />
      {error && <p id={`${id}-error`}>{error.message}</p>}
    </>
  )
}
