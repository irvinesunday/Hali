import { NextRequest, NextResponse } from 'next/server'

export const runtime = 'nodejs'

const MAX_EMAIL_LENGTH = 254
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

const ALLOWED_CATEGORIES = ['roads', 'water', 'electricity', 'transport', 'other'] as const
type Category = (typeof ALLOWED_CATEGORIES)[number]

const MAX_NAME = 120
const MAX_ORGANISATION = 200
const MAX_ROLE = 120
const MAX_AREA = 200
const MAX_MESSAGE = 500

interface InquiryPayload {
  name: string
  organisation: string
  role: string
  email: string
  area: string
  category: Category
  message?: string
}

export async function POST(request: NextRequest) {
  let raw: Record<string, unknown>
  try {
    raw = await request.json()
  } catch {
    return NextResponse.json({ success: false, error: 'Invalid request' }, { status: 400 })
  }

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

  let message: string | undefined
  if (messageRaw !== undefined) {
    if (typeof messageRaw !== 'string') {
      return NextResponse.json({ success: false, error: 'Invalid submission' }, { status: 400 })
    }
    const trimmed = messageRaw.trim()
    if (trimmed.length > MAX_MESSAGE) {
      return NextResponse.json({ success: false, error: 'Invalid submission' }, { status: 400 })
    }
    message = trimmed || undefined
  }

  const backendUrl = process.env.HALI_API_URL
  if (!backendUrl) {
    console.error('[inquiry] HALI_API_URL is not configured')
    return NextResponse.json({ success: false, error: 'Service unavailable' }, { status: 503 })
  }

  const body: InquiryPayload & { at?: string } = {
    name,
    organisation,
    role,
    email: emailRaw,
    area,
    category: category as Category,
    ...(message !== undefined ? { message } : {}),
  }

  let persistOk = false
  try {
    const res = await fetch(`${backendUrl}/v1/marketing/inquiries`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    })
    if (res.ok) {
      persistOk = true
    } else {
      const resBody = await res.text().catch(() => '')
      console.error('[inquiry] backend persistence non-2xx', res.status, resBody.slice(0, 500))
      if (res.status === 400) {
        return NextResponse.json({ success: false, error: 'Invalid submission' }, { status: 400 })
      }
      if (res.status === 429) {
        return NextResponse.json({ success: false, error: 'Too many requests' }, { status: 429 })
      }
      return NextResponse.json({ success: false, error: 'Storage unavailable' }, { status: 502 })
    }
  } catch (err) {
    console.error('[inquiry] backend persistence failed', err)
    return NextResponse.json({ success: false, error: 'Storage unavailable' }, { status: 502 })
  }

  // Email notification is best-effort. Inquiry is already durably persisted above.
  if (persistOk) {
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
          const resBody = await res.text().catch(() => '')
          console.warn('[inquiry] resend delivery non-2xx', res.status, resBody.slice(0, 500))
        }
      } catch (err) {
        console.warn('[inquiry] resend delivery failed', err)
      }
    }
  }

  return NextResponse.json({ success: true })
}
