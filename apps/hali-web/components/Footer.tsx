import Link from 'next/link'

const PRODUCT_LINKS = [
  { label: 'How It Works', href: '/how-it-works' },
  { label: 'For Institutions', href: '/for-institutions' },
  { label: 'About', href: '/about' },
]

const LEGAL_LINKS = [
  { label: 'Privacy', href: '/privacy' },
  { label: 'Terms', href: '/terms' },
]

export default function Footer() {
  return (
    <footer className="bg-hali-foreground text-hali-background">
      <div className="max-w-6xl mx-auto px-6 py-16">
        {/* Main grid */}
        <div className="flex flex-col space-y-8 md:flex-row md:space-y-0 md:justify-between">
          {/* Left — wordmark + tagline */}
          <div className="space-y-3">
            <span className="font-display text-2xl font-normal">Hali</span>
            <p className="text-sm text-hali-muted max-w-xs leading-relaxed">
              Civic infrastructure for real-world cities.
              <br />
              Know what&apos;s happening in your area — as it happens.
            </p>
          </div>

          {/* Right — nav groups */}
          <div className="grid grid-cols-2 gap-10">
            {/* Product */}
            <div>
              <p className="text-xs uppercase tracking-wide text-hali-muted mb-4 font-medium">
                Product
              </p>
              <ul className="space-y-2" role="list">
                {PRODUCT_LINKS.map((link) => (
                  <li key={link.href}>
                    <Link
                      href={link.href}
                      className="text-sm text-hali-background/80 hover:text-hali-background transition-colors"
                    >
                      {link.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>

            {/* Legal */}
            <div>
              <p className="text-xs uppercase tracking-wide text-hali-muted mb-4 font-medium">
                Legal
              </p>
              <ul className="space-y-2" role="list">
                {LEGAL_LINKS.map((link) => (
                  <li key={link.href}>
                    <Link
                      href={link.href}
                      className="text-sm text-hali-background/80 hover:text-hali-background transition-colors"
                    >
                      {link.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          </div>
        </div>

        {/* Bottom bar */}
        <div className="mt-14 pt-6 border-t border-hali-background/10">
          <p className="text-xs text-hali-muted">
            &copy; 2026 Hali. All rights reserved.
          </p>
        </div>
      </div>
    </footer>
  )
}
