import { useForm } from 'react-hook-form'
import { AuthFormError, type Credentials } from './api'
import { UNEXPECTED_ERROR } from './authErrors'

type Props = {
  submitLabel: string
  passwordAutoComplete: 'current-password' | 'new-password'
  onSubmit: (values: Credentials) => Promise<void>
}

export function CredentialsForm({ submitLabel, passwordAutoComplete, onSubmit }: Props) {
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Credentials>({ defaultValues: { email: '', password: '' } })

  const submit = handleSubmit(async (values) => {
    try {
      await onSubmit(values)
    } catch (error) {
      if (!(error instanceof AuthFormError)) {
        setError('root.server', { message: UNEXPECTED_ERROR })
        return
      }
      for (const [field, message] of Object.entries(error.errors.fields)) {
        setError(field as keyof Credentials, { message })
      }
      if (error.errors.form) {
        setError('root.server', { message: error.errors.form })
      }
    }
  })

  return (
    <form onSubmit={submit} noValidate>
      {errors.root?.server && <p role="alert">{errors.root.server.message}</p>}

      <label htmlFor="email">Email</label>
      <input
        id="email"
        type="email"
        autoComplete="email"
        aria-invalid={errors.email ? true : undefined}
        aria-describedby={errors.email ? 'email-error' : undefined}
        {...register('email', { required: 'Email is required.' })}
      />
      {errors.email && <p id="email-error">{errors.email.message}</p>}

      <label htmlFor="password">Password</label>
      <input
        id="password"
        type="password"
        autoComplete={passwordAutoComplete}
        aria-invalid={errors.password ? true : undefined}
        aria-describedby={errors.password ? 'password-error' : undefined}
        {...register('password', { required: 'Password is required.' })}
      />
      {errors.password && <p id="password-error">{errors.password.message}</p>}

      <button type="submit" disabled={isSubmitting}>
        {submitLabel}
      </button>
    </form>
  )
}
