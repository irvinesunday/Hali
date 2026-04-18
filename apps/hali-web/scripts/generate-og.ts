// Generates apps/hali-web/public/og-image.png — a 1200×630 OpenGraph card.
//
// Usage: pnpm --filter @hali/hali-web exec tsx scripts/generate-og.ts
// Or:    node -r tsx/cjs apps/hali-web/scripts/generate-og.ts
//
// Re-run when the wordmark, tagline, or brand palette changes.
// The generated PNG is committed to git — this script is the source of truth.

import sharp from 'sharp'
import path from 'node:path'

// Brand palette (approximate sRGB translations of OKLCH tokens in
// packages/design-system/src/tokens/colors.ts and
// apps/hali-web/tailwind.config.ts). These are baked into the SVG string as
// librsvg does not reliably render OKLCH.
const BG = '#f5f9fa' // hali-background — warm off-white
const FG = '#252f3a' // hali-foreground — deep blue-grey
const MUTED = '#4a5562' // near hali-muted-foreground
const TEAL = '#128893' // hali-primary — citizen teal

const svg = `
<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="630" viewBox="0 0 1200 630">
  <rect width="1200" height="630" fill="${BG}"/>
  <rect x="0" y="0" width="14" height="630" fill="${TEAL}"/>
  <g>
    <text x="100" y="310"
          font-family="Georgia, 'Times New Roman', serif"
          font-size="200" font-weight="700" fill="${FG}"
          letter-spacing="-2">Hali</text>
    <text x="100" y="410"
          font-family="Helvetica, Arial, sans-serif"
          font-size="44" font-weight="400" fill="${MUTED}">
      What's happening in your area?
    </text>
    <text x="100" y="555"
          font-family="Helvetica, Arial, sans-serif"
          font-size="26" font-weight="500" fill="${TEAL}"
          letter-spacing="1">gethali.app</text>
  </g>
</svg>
`

async function main() {
  const out = path.resolve(__dirname, '..', 'public', 'og-image.png')
  await sharp(Buffer.from(svg))
    .png({ compressionLevel: 9 })
    .toFile(out)
  // eslint-disable-next-line no-console
  console.log(`wrote ${out}`)
}

main().catch((err) => {
  // eslint-disable-next-line no-console
  console.error(err)
  process.exit(1)
})
