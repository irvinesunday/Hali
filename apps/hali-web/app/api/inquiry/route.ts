import { NextRequest, NextResponse } from 'next/server'
import { neon } from '@neondatabase/serverless'

// RFC 5321 caps a full email at 254 chars; reject longer inputs before the
// regex runs so an adversarial string can't trigger polynomial backtracking.
const MAX_EMAIL_LENGTH = 254
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

const ALLOWED_CATEGORIES = ['roads', 'water', 'electricity', 'transport', 'other'] as const
type Category = (typeof ALLOWED_CATEGORIES)[number]

// Server-side upper bounds for free-text fields. Keep modest so a single
// adversarial request can't bloat persisted data or the notification email.
const MAX_NAME = 120
const MAX_ORGANISATION = 200
const MAX_ROLE = 120
const MAX_AREA = 200
const MAX_MESSAGE = 500

// Schema initialization guard — runs CREATE TABLE at most once per instance
// (cold start). Subsequent requests within the same instance skip DDL entirely,
// avoiding extra latency and DDL lock acquisition on every request.
let schemaReady = false

export async function POST(request: NextRequest) {
  let raw: Record<string, unknown>
  try {
    raw = await request.json()
  } catch {
    return NextResponse.json({ success: false, error: 'Invalid request' }, { status: 400 })
  }

  // Validate and sanitise required fields
  const name = typeof raw.name === 'string' ? raw.name.trim() : ''
  const organisation = typeof raw.organisation === 'string' ? raw.organisation.trim() : ''
  const role = typeof raw.role === 'string' ? raw.role.trim() : ''
  const emailRaw = typeof raw.email === 'string' ? raw.email.trim().toLowerCase() : ''
  const area = typeof raw.area === 'string' ? raw.area.trim() : ''
  const category = typeof raw.category === 'string' ? raw.category.trim() : ''
  const messageRaw = raw.message !== undefined ? raw.message : undefined

  const isValidCategory = (ALLOWED_CATEGORIES as readonly string[]).includes(category)

  const isValid =
    name.length >= 2 &&
    name.length <= MAX_NAME &&
    organisation.length >= 1 &&
    organisation.length <= MAX_ORGANISATION &&
    role.length >= 1 &&
    role.length <= MAX_ROLE &&
    emailRaw.length > 0 &&
    emailRaw.length <= MAX_EMAIL_LENGTH &&
    EMAIL_RE.test(emailRaw) &&
    area.length >= 1 &&
    area.length <= MAX_AREA &&
    isValidCategory

  if (!isValid) {
    return NextResponse.json({ success: false, error: 'Invalid submission' }, { status: 400 })
  }

  // Validate optional message field
  let message: string | null = null
  if (messageRaw !== undefined) {
    if (typeof messageRaw !== 'string') {
      return NextResponse.json({ success: false, error: 'Invalid submission' }, { status: 400 })
    }
    const trimmed = messageRaw.trim()
    if (trimmed.length > MAX_MESSAGE) {
      return NextResponse.json({ success: false, error: 'Invalid submission' }, { status: 400 })
    }
    message = trimmed || null
  }

  // Persist first — if this fails, return error immediately without attempting email.
  try {
    const url = process.env.DATABASE_URL
    if (!url) throw new Error('DATABASE_URL is not set')
    const sql = neon(url)
    if (!schemaReady) {
      await sql`
        CREATE TABLE IF NOT EXISTS pilot_inquiries (
          id           BIGSERIAL    PRIMARY KEY,
          name         TEXT         NOT NULL,
          organisation TEXT         NOT NULL,
          role         TEXT         NOT NULL,
          email        TEXT         NOT NULL,
          area         TEXT         NOT NULL,
          category     TEXT         NOT NULL,
          message      TEXT,
          created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
        )
      `
      schemaReady = true
    }
    await sql`
      INSERT INTO pilot_inquiries (name, organisation, role, email, area, category, message)
      VALUES (${name}, ${organisation}, ${role}, ${emailRaw}, ${area}, ${category as Category}, ${message})
    `
  } catch (err) {
    console.error('[inquiry] persistence failed', err)
    return NextResponse.json({ success: false, error: 'Storage unavailable' }, { status: 500 })
  }

  const resendApiKey = process.env.RESEND_API_KEY
  const inquiryEmail = process.env.INQUIRY_EMAIL
  if (resendApiKey && inquiryEmail) {
    try {
      const emailText = [
        `Name: ${name}`,
        `Organisation: ${organisation}`,
        `Role: ${role}`,
        `Email: ${emailRaw}`,
        `Area: ${area}`,
        `Category: ${category}`,
        `Message: ${message ?? 'None'}`,
      ].join('\n')

      const res = await fetch('https://api.resend.com/emails', {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${resendApiKey}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          from: 'Hali <noreply@gethali.app>',
          to: [inquiryEmail],
          subject: `New pilot inquiry — ${organisation} (${category})`,
          text: emailText,
        }),
      })
      if (!res.ok) {
        const body = await res.text().catch(() => '')
        console.warn('[inquiry] resend delivery non-2xx', res.status, body.slice(0, 500))
      }
    } catch (err) {
      // Email delivery is best-effort; inquiry is already persisted.
      console.warn('[inquiry] resend delivery failed', err)
    }
  } else {
    console.log('[inquiry] No Resend config — persisted to Postgres only')
  }

  return NextResponse.json({ success: true })
}
