import { NextRequest, NextResponse } from 'next/server'
import { promises as fs } from 'node:fs'
import path from 'node:path'

// RFC 5321 caps a full email at 254 chars; reject longer inputs before the
// regex runs so an adversarial string can't trigger polynomial backtracking.
const MAX_EMAIL_LENGTH = 254
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

async function persistSignup(email: string) {
  // Append to data/signups.json — create-if-missing, preserves prior signups.
  const dataDir = path.resolve(process.cwd(), 'data')
  const file = path.join(dataDir, 'signups.json')
  await fs.mkdir(dataDir, { recursive: true })
  let existing: Array<{ email: string; at: string }> = []
  try {
    const raw = await fs.readFile(file, 'utf8')
    const parsed = JSON.parse(raw)
    if (!Array.isArray(parsed)) {
      // File exists but has an unexpected shape. Refuse to overwrite — this
      // likely means something else wrote to the file; surfacing the error
      // is safer than silently replacing prior data.
      throw new Error('signups.json is not an array')
    }
    existing = parsed
  } catch (err) {
    const code = (err as NodeJS.ErrnoException | null)?.code
    // Only treat "file does not exist yet" as empty state. Any other failure
    // (corruption, permission, I/O) re-throws so we don't clobber prior data.
    if (code !== 'ENOENT') throw err
  }
  existing.push({ email, at: new Date().toISOString() })
  await fs.writeFile(file, JSON.stringify(existing, null, 2), 'utf8')
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
    await persistSignup(email)
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

export const runtime = 'nodejs'
