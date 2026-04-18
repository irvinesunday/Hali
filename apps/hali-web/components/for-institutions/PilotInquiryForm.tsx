'use client'

import { useState, useRef, useEffect } from 'react'

type FormState = 'idle' | 'loading' | 'success' | 'error'

type Category = 'roads' | 'water' | 'electricity' | 'transport' | 'other'

interface FormFields {
  name: string
  organisation: string
  role: string
  email: string
  area: string
  category: Category | ''
  message: string
}

const INITIAL_FIELDS: FormFields = {
  name: '',
  organisation: '',
  role: '',
  email: '',
  area: '',
  category: '',
  message: '',
}

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

function validate(fields: FormFields): Partial<Record<keyof FormFields, string>> {
  const errors: Partial<Record<keyof FormFields, string>> = {}

  if (fields.name.trim().length < 2) {
    errors.name = 'Please enter your full name.'
  }
  if (fields.organisation.trim().length < 1) {
    errors.organisation = 'Please enter your organisation name.'
  }
  if (fields.role.trim().length < 1) {
    errors.role = 'Please enter your role.'
  }
  const email = fields.email.trim().toLowerCase()
  if (!email || email.length > 254 || !EMAIL_RE.test(email)) {
    errors.email = 'Please enter a valid email address.'
  }
  if (fields.area.trim().length < 1) {
    errors.area = 'Please enter your area of operation.'
  }
  if (!fields.category) {
    errors.category = 'Please select a category.'
  }
  if (fields.message.trim().length > 500) {
    errors.message = 'Message must be 500 characters or fewer.'
  }

  return errors
}

export default function PilotInquiryForm() {
  const [formState, setFormState] = useState<FormState>('idle')
  const [fields, setFields] = useState<FormFields>(INITIAL_FIELDS)
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<keyof FormFields, string>>>({})
  const successHeadingRef = useRef<HTMLHeadingElement>(null)

  useEffect(() => {
    if (formState === 'success') {
      successHeadingRef.current?.focus()
    }
  }, [formState])

  function handleChange(
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>
  ) {
    const { name, value } = e.target
    setFields((prev) => ({ ...prev, [name]: value }))
    // Clear the error for the field being edited
    if (fieldErrors[name as keyof FormFields]) {
      setFieldErrors((prev) => ({ ...prev, [name]: undefined }))
    }
  }

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()

    const errors = validate(fields)
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors)
      return
    }

    setFormState('loading')

    try {
      const body: Record<string, string> = {
        name: fields.name.trim(),
        organisation: fields.organisation.trim(),
        role: fields.role.trim(),
        email: fields.email.trim().toLowerCase(),
        area: fields.area.trim(),
        category: fields.category as Category,
      }
      if (fields.message.trim()) {
        body.message = fields.message.trim()
      }

      const res = await fetch('/api/inquiry', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      })

      const data = (await res.json()) as { success: boolean }
      if (res.ok && data.success) {
        setFormState('success')
      } else {
        setFormState('error')
      }
    } catch {
      setFormState('error')
    }
  }

  const inputBase =
    'w-full rounded-lg border border-hali-border bg-hali-surface px-4 py-2.5 text-hali-foreground placeholder:text-hali-muted-foreground focus:outline-none focus:ring-2 focus:ring-hali-primary focus:border-hali-primary transition-colors'

  if (formState === 'success') {
    return (
      <section className="bg-hali-muted py-20 md:py-28 px-6">
        <div className="max-w-2xl mx-auto bg-hali-card rounded-2xl border border-hali-border p-8 md:p-10 flex flex-col items-center text-center gap-4">
          <svg
            width={48}
            height={48}
            viewBox="0 0 48 48"
            fill="none"
            stroke="currentColor"
            strokeWidth={2}
            strokeLinecap="round"
            strokeLinejoin="round"
            className="text-hali-primary"
            aria-hidden="true"
          >
            <circle cx="24" cy="24" r="20" />
            <polyline points="16 24 22 30 32 18" />
          </svg>
          <h2
            ref={successHeadingRef}
            tabIndex={-1}
            className="font-display text-3xl md:text-4xl text-hali-foreground focus:outline-none"
          >
            Thank you
          </h2>
          <p className="text-hali-muted-foreground">
            We&apos;ve received your inquiry. We&apos;ll be in touch within 48 hours.
          </p>
        </div>
      </section>
    )
  }

  return (
    <section className="bg-hali-muted py-20 md:py-28 px-6">
      <div className="max-w-2xl mx-auto bg-hali-card rounded-2xl border border-hali-border p-8 md:p-10">
        <h2 className="font-display text-3xl md:text-4xl text-hali-foreground text-center">
          Become a pilot partner
        </h2>
        <p className="mt-3 text-center text-hali-muted-foreground max-w-xl mx-auto">
          Tell us about your organisation. We&apos;ll reach out within 48 hours.
        </p>

        <form onSubmit={handleSubmit} noValidate className="mt-8">
          <div className="grid gap-5">
            {/* Name */}
            <div>
              <label htmlFor="inq-name" className="block text-sm font-medium text-hali-foreground mb-1.5">
                Name<span aria-hidden="true" className="text-hali-muted-foreground"> *</span>
              </label>
              <input
                id="inq-name"
                name="name"
                type="text"
                autoComplete="name"
                value={fields.name}
                onChange={handleChange}
                aria-required="true"
                aria-invalid={!!fieldErrors.name}
                aria-describedby={fieldErrors.name ? 'inq-name-error' : undefined}
                className={inputBase}
                placeholder="Your full name"
              />
              {fieldErrors.name && (
                <p id="inq-name-error" className="mt-1.5 text-sm text-hali-destructive">
                  {fieldErrors.name}
                </p>
              )}
            </div>

            {/* Organisation */}
            <div>
              <label htmlFor="inq-organisation" className="block text-sm font-medium text-hali-foreground mb-1.5">
                Organisation<span aria-hidden="true" className="text-hali-muted-foreground"> *</span>
              </label>
              <input
                id="inq-organisation"
                name="organisation"
                type="text"
                autoComplete="organization"
                value={fields.organisation}
                onChange={handleChange}
                aria-required="true"
                aria-invalid={!!fieldErrors.organisation}
                aria-describedby={fieldErrors.organisation ? 'inq-organisation-error' : undefined}
                className={inputBase}
                placeholder="Your organisation name"
              />
              {fieldErrors.organisation && (
                <p id="inq-organisation-error" className="mt-1.5 text-sm text-hali-destructive">
                  {fieldErrors.organisation}
                </p>
              )}
            </div>

            {/* Role */}
            <div>
              <label htmlFor="inq-role" className="block text-sm font-medium text-hali-foreground mb-1.5">
                Role<span aria-hidden="true" className="text-hali-muted-foreground"> *</span>
              </label>
              <input
                id="inq-role"
                name="role"
                type="text"
                autoComplete="organization-title"
                value={fields.role}
                onChange={handleChange}
                aria-required="true"
                aria-invalid={!!fieldErrors.role}
                aria-describedby={fieldErrors.role ? 'inq-role-error' : undefined}
                className={inputBase}
                placeholder="Your job title or role"
              />
              {fieldErrors.role && (
                <p id="inq-role-error" className="mt-1.5 text-sm text-hali-destructive">
                  {fieldErrors.role}
                </p>
              )}
            </div>

            {/* Email */}
            <div>
              <label htmlFor="inq-email" className="block text-sm font-medium text-hali-foreground mb-1.5">
                Email<span aria-hidden="true" className="text-hali-muted-foreground"> *</span>
              </label>
              <input
                id="inq-email"
                name="email"
                type="email"
                autoComplete="email"
                value={fields.email}
                onChange={handleChange}
                aria-required="true"
                aria-invalid={!!fieldErrors.email}
                aria-describedby={fieldErrors.email ? 'inq-email-error' : undefined}
                className={inputBase}
                placeholder="you@organisation.com"
              />
              {fieldErrors.email && (
                <p id="inq-email-error" className="mt-1.5 text-sm text-hali-destructive">
                  {fieldErrors.email}
                </p>
              )}
            </div>

            {/* Area */}
            <div>
              <label htmlFor="inq-area" className="block text-sm font-medium text-hali-foreground mb-1.5">
                Area of operation<span aria-hidden="true" className="text-hali-muted-foreground"> *</span>
              </label>
              <input
                id="inq-area"
                name="area"
                type="text"
                value={fields.area}
                onChange={handleChange}
                aria-required="true"
                aria-invalid={!!fieldErrors.area}
                aria-describedby={fieldErrors.area ? 'inq-area-error' : undefined}
                className={inputBase}
                placeholder="e.g. Nairobi Central, Lagos Island"
              />
              {fieldErrors.area && (
                <p id="inq-area-error" className="mt-1.5 text-sm text-hali-destructive">
                  {fieldErrors.area}
                </p>
              )}
            </div>

            {/* Category */}
            <div>
              <label htmlFor="inq-category" className="block text-sm font-medium text-hali-foreground mb-1.5">
                Primary civic category<span aria-hidden="true" className="text-hali-muted-foreground"> *</span>
              </label>
              <select
                id="inq-category"
                name="category"
                value={fields.category}
                onChange={handleChange}
                aria-required="true"
                aria-invalid={!!fieldErrors.category}
                aria-describedby={fieldErrors.category ? 'inq-category-error' : undefined}
                className={inputBase}
              >
                <option value="" disabled>Select a category</option>
                <option value="roads">Roads</option>
                <option value="water">Water</option>
                <option value="electricity">Electricity</option>
                <option value="transport">Transport</option>
                <option value="other">Other</option>
              </select>
              {fieldErrors.category && (
                <p id="inq-category-error" className="mt-1.5 text-sm text-hali-destructive">
                  {fieldErrors.category}
                </p>
              )}
            </div>

            {/* Message */}
            <div>
              <label htmlFor="inq-message" className="block text-sm font-medium text-hali-foreground mb-1.5">
                Anything else you&apos;d like us to know
              </label>
              <textarea
                id="inq-message"
                name="message"
                value={fields.message}
                onChange={handleChange}
                aria-invalid={!!fieldErrors.message}
                aria-describedby={fieldErrors.message ? 'inq-message-error' : undefined}
                className={`${inputBase} min-h-[120px] resize-y`}
                placeholder="Optional — share any context about your use case, existing systems, or questions."
              />
              <p className="mt-1 text-xs text-hali-muted-foreground">{fields.message.length}/500</p>
              {fieldErrors.message && (
                <p id="inq-message-error" className="mt-1.5 text-sm text-hali-destructive">
                  {fieldErrors.message}
                </p>
              )}
            </div>
          </div>

          {formState === 'error' && (
            <div className="mt-5 rounded-lg border border-hali-destructive/40 bg-hali-destructive/5 px-4 py-3 text-sm text-hali-destructive">
              Something went wrong. Please try again, or email us directly at hello@gethali.app
            </div>
          )}

          <button
            type="submit"
            disabled={formState === 'loading'}
            className="mt-6 w-full inline-flex items-center justify-center rounded-full font-medium transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 text-base px-7 py-3.5 bg-hali-primary text-hali-primary-foreground hover:opacity-90 focus-visible:ring-hali-primary disabled:opacity-60 disabled:cursor-not-allowed"
          >
            {formState === 'loading' ? 'Submitting\u2026' : 'Submit Pilot Inquiry'}
          </button>
        </form>
      </div>
    </section>
  )
}
