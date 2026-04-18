import { NextRequest, NextResponse } from 'next/server'
import { appendFileSync, mkdirSync } from 'node:fs'
import path from 'node:path'

export const runtime = 'nodejs'

// RFC 5321 caps a full email at 254 chars; reject longer inputs before the
// regex runs so an adversarial string can't trigger polynomial backtracking.
const MAX_EMAIL_LENGTH = 254
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

// Append-only NDJSON persistence — one JSON object per line. Eliminates the
// read-modify-write cycle that the previous JSON-array implementation used,
// which lost concurrent writes. Note: this is still not safe across multiple
// serverless instances (no inter-instance locking, and the filesystem is
// ephemeral on serverless). Acceptable for MVP / local / staging; production
// durability is a post-launch concern (database or queue).
function persistEntry(filename: string, entry: object): void {
  const dataDir = path.resolve(process.cwd(), 'data')
  mkdirSync(dataDir, { recursive: true })
  const filePath = path.join(dataDir, filename)
  appendFileSync(filePath, JSON.stringify(entry) + '\n', { encoding: 'utf8' })
}

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

  try {
    persistEntry('signups.ndjson', { email, at: new Date().toISOString() })
  } catch (err) {
    // Persistence is the primary contract — fail closed if we can't save.
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
        // fetch only throws on network errors; surface 4xx/5xx explicitly.
        const body = await res.text().catch(() => '')
        console.warn('[notify] resend delivery non-2xx', res.status, body.slice(0, 500))
      }
    } catch (err) {
      // Email delivery is best-effort; signup is already persisted.
      console.warn('[notify] resend delivery failed', err)
    }
  }

  return NextResponse.json({ success: true })
}
