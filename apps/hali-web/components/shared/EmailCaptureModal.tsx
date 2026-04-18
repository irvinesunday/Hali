'use client'

import { useEffect, useId, useRef, useState } from 'react'

interface Props {
  open: boolean
  onClose: () => void
}

type State = 'idle' | 'loading' | 'success' | 'error'

export default function EmailCaptureModal({ open, onClose }: Props) {
  const [email, setEmail] = useState('')
  const [state, setState] = useState<State>('idle')
  const headingId = useId()
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (!open) return
    // Auto-focus input on open
    const frame = requestAnimationFrame(() => {
      inputRef.current?.focus()
    })
    return () => cancelAnimationFrame(frame)
  }, [open])

  useEffect(() => {
    if (!open) return
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', handleKey)
    return () => document.removeEventListener('keydown', handleKey)
  }, [open, onClose])

  // Reset state when modal opens
  useEffect(() => {
    if (open) {
      setState('idle')
      setEmail('')
    }
  }, [open])

  if (!open) return null

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setState('loading')
    try {
      const res = await fetch('/api/notify', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email }),
      })
      if (res.ok) {
        setState('success')
      } else {
        setState('error')
      }
    } catch {
      setState('error')
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 bg-hali-foreground/40 backdrop-blur-sm flex items-center justify-center px-4"
      onClick={onClose}
      aria-hidden="true"
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={headingId}
        className="bg-hali-background rounded-2xl shadow-xl max-w-md w-full p-6 relative"
        onClick={(e) => e.stopPropagation()}
      >
        <button
          type="button"
          onClick={onClose}
          aria-label="Close"
          className="absolute top-4 right-4 text-hali-muted-foreground hover:text-hali-foreground text-2xl leading-none"
        >
          ×
        </button>

        {state === 'success' ? (
          <div className="py-4 text-center">
            <p className="text-hali-foreground">
              You&apos;re on the list. We&apos;ll be in touch when Hali launches.
            </p>
            <button
              type="button"
              onClick={onClose}
              className="mt-6 text-sm text-hali-primary hover:underline"
            >
              Close
            </button>
          </div>
        ) : (
          <>
            <h2
              id={headingId}
              className="font-display text-2xl text-hali-foreground"
            >
              Be first to know
            </h2>
            <p className="text-hali-muted-foreground text-sm mt-2">
              We&apos;ll let you know when Hali launches in your area.
            </p>

            <form onSubmit={handleSubmit} className="mt-6 space-y-4" noValidate>
              <input
                ref={inputRef}
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="Your email address"
                className="w-full rounded-lg border border-hali-border px-4 py-3 text-hali-foreground bg-hali-background placeholder:text-hali-muted-foreground focus:outline-none focus:ring-2 focus:ring-hali-primary"
              />

              {state === 'error' && (
                <p className="text-hali-destructive text-sm">
                  Something went wrong. Please try again.
                </p>
              )}

              <button
                type="submit"
                disabled={state === 'loading'}
                className="w-full bg-hali-primary text-hali-primary-foreground rounded-full px-5 py-3 text-sm font-medium transition-opacity hover:opacity-90 disabled:opacity-60"
              >
                {state === 'loading' ? 'Sending…' : 'Notify me'}
              </button>
            </form>
          </>
        )}
      </div>
    </div>
  )
}
