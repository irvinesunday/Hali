import { NextRequest, NextResponse } from 'next/server'

export const runtime = 'nodejs'

// RFC 5321 caps a full email at 254 chars; reject longer inputs before the
// regex runs so an adversarial string can't trigger polynomial backtracking.
const MAX_EMAIL_LENGTH = 254
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export async function POST(request: NextRequest) {
  let payload: { email?: unknown }
  try {
    payload = await request.json()
  } catch {
    return NextResponse.json({ success: false, error: 'Invalid request' }, { status: 400 })
  }

  const email = typeof payload.email === 'string' ? payload.email.trim().toLowerCase() : ''
  if (!email || email.length > MAX_EMAIL_LENGTH || !EMAIL_RE.test(email)) {
    return NextResponse.json({ success: false, error: 'Invalid email' }, { status: 400 })
  }

  const backendUrl = process.env.HALI_API_URL
  if (!backendUrl) {
    console.error('[notify] HALI_API_URL is not configured')
    return NextResponse.json({ success: false, error: 'Service unavailable' }, { status: 503 })
  }

  const clientIp = request.headers.get('x-forwarded-for') ?? request.headers.get('x-real-ip') ?? ''

  let persistOk = false
  try {
    const res = await fetch(`${backendUrl}/v1/marketing/signups`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(clientIp ? { 'X-Forwarded-For': clientIp } : {}),
      },
      body: JSON.stringify({ email }),
    })
    if (res.ok) {
      persistOk = true
    } else {
      const body = await res.text().catch(() => '')
      console.error('[notify] backend persistence non-2xx', res.status, body.slice(0, 500))
      if (res.status === 400) {
        return NextResponse.json({ success: false, error: 'Invalid submission' }, { status: 400 })
      }
      if (res.status === 429) {
        return NextResponse.json({ success: false, error: 'Too many requests' }, { status: 429 })
      }
      return NextResponse.json({ success: false, error: 'Storage unavailable' }, { status: 502 })
    }
  } catch (err) {
    console.error('[notify] backend persistence failed', err)
    return NextResponse.json({ success: false, error: 'Storage unavailable' }, { status: 502 })
  }

  // Email notification is best-effort. Signup is already durably persisted above.
  if (persistOk) {
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
        console.warn('[notify] resend delivery failed', err)
      }
    }
  }

  return NextResponse.json({ success: true })
}
