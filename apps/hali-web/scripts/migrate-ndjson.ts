#!/usr/bin/env tsx
/**
 * One-time migration: ingest existing NDJSON records into Postgres.
 * Run before deploying the Postgres-backed routes to production.
 * Safe to run multiple times — duplicate rows will be inserted if re-run,
 * so run once only after confirming the target tables are empty.
 *
 * Usage: npx tsx apps/hali-web/scripts/migrate-ndjson.ts
 *   (from repo root, with DATABASE_URL set in environment or apps/hali-web/.env.local)
 */

import { neon } from '@neondatabase/serverless'
import { readFileSync, existsSync } from 'fs'
import { join } from 'path'

// DATABASE_URL must be set in the shell environment before running this script.
// Example: DATABASE_URL=postgresql://... npx tsx apps/hali-web/scripts/migrate-ndjson.ts
const url = process.env.DATABASE_URL
if (!url) {
  console.error('DATABASE_URL is not set — cannot migrate')
  process.exit(1)
}

const sql = neon(url)

async function migrate() {
  await sql`
    CREATE TABLE IF NOT EXISTS email_signups (
      id         BIGSERIAL    PRIMARY KEY,
      email      TEXT         NOT NULL,
      created_at TIMESTAMPTZ  NOT NULL DEFAULT NOW()
    )
  `
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

  // Migrate signups — NDJSON records use field "at" for the timestamp
  const signupsPath = join(process.cwd(), 'apps/hali-web/data/signups.ndjson')
  if (existsSync(signupsPath)) {
    const lines = readFileSync(signupsPath, 'utf8')
      .split('\n')
      .filter(l => l.trim())
    console.log(`Migrating ${lines.length} signup record(s)...`)
    let migrated = 0
    for (const line of lines) {
      try {
        const record = JSON.parse(line) as { email?: string; at?: string }
        if (!record.email) {
          console.warn(`Skipping signup line with no email: ${line}`)
          continue
        }
        const createdAt = record.at ?? new Date().toISOString()
        await sql`
          INSERT INTO email_signups (email, created_at)
          VALUES (${record.email}, ${createdAt})
        `
        migrated++
      } catch (err) {
        console.warn(`Skipping malformed signup line: ${line}`, err)
      }
    }
    console.log(`✓ Signups migrated: ${migrated}/${lines.length}`)
  } else {
    console.log('No signups.ndjson found — skipping')
  }

  // Migrate inquiries — NDJSON records use field "at" for the timestamp
  const inquiriesPath = join(process.cwd(), 'apps/hali-web/data/inquiries.ndjson')
  if (existsSync(inquiriesPath)) {
    const lines = readFileSync(inquiriesPath, 'utf8')
      .split('\n')
      .filter(l => l.trim())
    console.log(`Migrating ${lines.length} inquiry record(s)...`)
    let migrated = 0
    for (const line of lines) {
      try {
        const r = JSON.parse(line) as {
          email?: string
          name?: string
          organisation?: string
          role?: string
          area?: string
          category?: string
          message?: string
          at?: string
        }
        if (!r.email) {
          console.warn(`Skipping inquiry line with no email: ${line}`)
          continue
        }
        const createdAt = r.at ?? new Date().toISOString()
        await sql`
          INSERT INTO pilot_inquiries
            (name, organisation, role, email, area, category, message, created_at)
          VALUES (
            ${r.name ?? ''},
            ${r.organisation ?? ''},
            ${r.role ?? ''},
            ${r.email},
            ${r.area ?? ''},
            ${r.category ?? 'other'},
            ${r.message ?? null},
            ${createdAt}
          )
        `
        migrated++
      } catch (err) {
        console.warn(`Skipping malformed inquiry line: ${line}`, err)
      }
    }
    console.log(`✓ Inquiries migrated: ${migrated}/${lines.length}`)
  } else {
    console.log('No inquiries.ndjson found — skipping')
  }

  console.log('Migration complete.')
}

migrate().catch(err => {
  console.error('Migration failed:', err)
  process.exit(1)
})
