import { NextRequest, NextResponse } from 'next/server'
import { neon } from '@neondatabase/serverless'

// RFC 5321 caps a full email at 254 chars; reject longer inputs before the
// regex runs so an adversarial string can't trigger polynomial backtracking.
const MAX_EMAIL_LENGTH = 254
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

// Schema initialization guard — runs CREATE TABLE at most once per instance
// (cold start). Subsequent requests within the same instance skip DDL entirely,
// avoiding extra latency and DDL lock acquisition on every request.
let schemaReady = false

export async function POST(request: NextRequest) {
  let payload: { email?: string }
  try {
    payload = await request.json()
  } catch {
    return NextResponse.json({ success: false, error: 'Invalid request' }, { status: 400 })
  }
  const email = (payload.email ?? '').trim().toLowerCase()
  if (!email || email.length > MAX_EMAIL_LENGTH || !EMAIL_RE.test(email)) {
    return NextResponse.json({ success: false, error: 'Invalid email' }, { status: 400 })
  }

  // Persist first — if this fails, return error immediately without attempting email.
  try {
    const url = process.env.DATABASE_URL
    if (!url) throw new Error('DATABASE_URL is not set')
    const sql = neon(url)
    if (!schemaReady) {
      await sql`
        CREATE TABLE IF NOT EXISTS email_signups (
          id         BIGSERIAL    PRIMARY KEY,
          email      TEXT         NOT NULL,
          created_at TIMESTAMPTZ  NOT NULL DEFAULT NOW()
        )
      `
      schemaReady = true
    }
    await sql`INSERT INTO email_signups (email) VALUES (${email})`
  } catch (err) {
    console.error('[notify] persistence failed', err)
    return NextResponse.json({ success: false, error: 'Storage unavailable' }, { status: 500 })
  }

  const resendApiKey = process.env.RESEND_API_KEY
  const notifyEmail = process.env.NOTIFY_EMAIL
  if (resendApiKey && notifyEmail) {
    try {
      const res = await fetch('https://api.resend.com/emails', {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${resendApiKey}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          from: 'Hali <noreply@gethali.app>',
          to: [notifyEmail],
          subject: 'New launch notification signup',
          text: `New signup: ${email}`,
        }),
      })
      if (!res.ok) {
        const body = await res.text().catch(() => '')
        console.warn('[notify] resend delivery non-2xx', res.status, body.slice(0, 500))
      }
    } catch (err) {
      // Email delivery is best-effort; signup is already persisted.
      console.warn('[notify] resend delivery failed', err)
    }
  } else {
    console.log('[notify] No Resend config — persisted to Postgres only')
  }

  return NextResponse.json({ success: true })
}
