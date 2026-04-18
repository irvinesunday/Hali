import { NextRequest, NextResponse } from 'next/server'
import { promises as fs } from 'node:fs'
import path from 'node:path'

export const runtime = 'nodejs'

// RFC 5321 caps a full email at 254 chars; reject longer inputs before the
// regex runs so an adversarial string can't trigger polynomial backtracking.
const MAX_EMAIL_LENGTH = 254
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

const ALLOWED_CATEGORIES = ['roads', 'water', 'electricity', 'transport', 'other'] as const
type Category = (typeof ALLOWED_CATEGORIES)[number]

interface InquiryPayload {
  name: string
  organisation: string
  role: string
  email: string
  area: string
  category: Category
  message?: string
}

async function persistInquiry(payload: InquiryPayload & { at: string }) {
  // Append to data/inquiries.json — create-if-missing, preserves prior inquiries.
  const dataDir = path.resolve(process.cwd(), 'data')
  const file = path.join(dataDir, 'inquiries.json')
  await fs.mkdir(dataDir, { recursive: true })
  let existing: Array<InquiryPayload & { at: string }> = []
  try {
    const raw = await fs.readFile(file, 'utf8')
    const parsed = JSON.parse(raw)
    if (!Array.isArray(parsed)) {
      // File exists but has an unexpected shape. Refuse to overwrite — this
      // likely means something else wrote to the file; surfacing the error
      // is safer than silently replacing prior data.
      throw new Error('inquiries.json is not an array')
    }
    existing = parsed
  } catch (err) {
    const code = (err as NodeJS.ErrnoException | null)?.code
    // Only treat "file does not exist yet" as empty state. Any other failure
    // (corruption, permission, I/O) re-throws so we don't clobber prior data.
    if (code !== 'ENOENT') throw err
  }
  existing.push(payload)
  await fs.writeFile(file, JSON.stringify(existing, null, 2), 'utf8')
}

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
    organisation.length >= 1 &&
    role.length >= 1 &&
    emailRaw.length > 0 &&
    emailRaw.length <= MAX_EMAIL_LENGTH &&
    EMAIL_RE.test(emailRaw) &&
    area.length >= 1 &&
    isValidCategory

  if (!isValid) {
    return NextResponse.json({ success: false, error: 'Invalid submission' }, { status: 400 })
  }

  // Validate optional message field
  let message: string | undefined
  if (messageRaw !== undefined) {
    if (typeof messageRaw !== 'string') {
      return NextResponse.json({ success: false, error: 'Invalid submission' }, { status: 400 })
    }
    const trimmed = messageRaw.trim()
    if (trimmed.length > 500) {
      return NextResponse.json({ success: false, error: 'Invalid submission' }, { status: 400 })
    }
    message = trimmed || undefined
  }

  const payload: InquiryPayload & { at: string } = {
    name,
    organisation,
    role,
    email: emailRaw,
    area,
    category: category as Category,
    ...(message !== undefined ? { message } : {}),
    at: new Date().toISOString(),
  }

  try {
    await persistInquiry(payload)
  } catch (err) {
    // Persistence is the primary contract — fail closed if we can't save.
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
        // fetch only throws on network errors; surface 4xx/5xx explicitly.
        const body = await res.text().catch(() => '')
        console.warn('[inquiry] resend delivery non-2xx', res.status, body.slice(0, 500))
      }
    } catch (err) {
      // Email delivery is best-effort; inquiry is already persisted.
      console.warn('[inquiry] resend delivery failed', err)
    }
  }

  return NextResponse.json({ success: true })
}
