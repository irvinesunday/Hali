// Tailwind configuration for apps/hali-web.
//
// Color tokens are inlined as OKLCH values rather than imported from
// @hali/design-system at config-evaluation time. The design-system
// package is pure TypeScript source (no CJS build) and Tailwind
// config runs in Node via require(), so a dynamic ESM import here
// would break the build. The OKLCH values below are copied verbatim
// from packages/design-system/src/tokens/colors.ts (CitizenTheme =
// SharedSemanticColors + CitizenColors). Update both files together
// when the token set changes.

import type { Config } from 'tailwindcss'

const config: Config = {
  content: [
    './app/**/*.{ts,tsx}',
    './components/**/*.{ts,tsx}',
    '../../packages/design-system/src/**/*.{ts,tsx}',
  ],
  theme: {
    extend: {
      colors: {
        hali: {
          // CitizenColors
          background: 'oklch(0.98 0.005 200)',
          foreground: 'oklch(0.25 0.02 220)',
          text: 'oklch(0.25 0.02 220)',
          primary: 'oklch(0.55 0.12 190)',
          primaryForeground: 'oklch(0.99 0 0)',
          'primary-foreground': 'oklch(0.99 0 0)',
          secondary: 'oklch(0.94 0.01 200)',
          secondaryForeground: 'oklch(0.35 0.02 220)',
          'secondary-foreground': 'oklch(0.35 0.02 220)',
          accent: 'oklch(0.92 0.02 180)',
          accentForeground: 'oklch(0.30 0.02 220)',
          'accent-foreground': 'oklch(0.30 0.02 220)',
          destructive: 'oklch(0.60 0.15 30)',
          destructiveForeground: 'oklch(0.99 0 0)',
          ring: 'oklch(0.55 0.12 190)',
          // SharedSemanticColors
          surface: 'oklch(1 0 0)',
          card: 'oklch(1 0 0)',
          cardForeground: 'oklch(0.225 0.02 210)',
          'card-foreground': 'oklch(0.225 0.02 210)',
          popover: 'oklch(1 0 0)',
          popoverForeground: 'oklch(0.225 0.02 210)',
          border: 'oklch(0.91 0.01 190)',
          input: 'oklch(0.92 0.01 190)',
          muted: 'oklch(0.955 0.007 190)',
          mutedForeground: 'oklch(0.5 0.02 210)',
          'muted-foreground': 'oklch(0.5 0.02 210)',
        },
      },
      fontFamily: {
        display: ['var(--font-display)', 'serif'],
        body: ['var(--font-body)', 'sans-serif'],
      },
    },
  },
  plugins: [],
}

export default config
