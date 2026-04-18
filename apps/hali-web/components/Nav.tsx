'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { useEffect, useState } from 'react'

const NAV_LINKS = [
  { label: 'How It Works', href: '/how-it-works' },
  { label: 'For Institutions', href: '/for-institutions' },
  { label: 'About', href: '/about' },
]

function CtaButton() {
  const isLive = process.env.NEXT_PUBLIC_APP_LAUNCH_STATE === 'live'
  const label = isLive ? 'Open Hali' : 'Get notified at launch'
  return (
    <button
      type="button"
      className="bg-hali-primary text-hali-primary-foreground rounded-full px-5 py-2 text-sm font-medium transition-opacity hover:opacity-90"
    >
      {label}
    </button>
  )
}

interface MobileDrawerProps {
  open: boolean
  onClose: () => void
  pathname: string
}

function MobileDrawer({ open, onClose, pathname }: MobileDrawerProps) {
  useEffect(() => {
    if (!open) return
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', handleKey)
    return () => document.removeEventListener('keydown', handleKey)
  }, [open, onClose])

  useEffect(() => {
    if (open) {
      document.body.style.overflow = 'hidden'
    } else {
      document.body.style.overflow = ''
    }
    return () => {
      document.body.style.overflow = ''
    }
  }, [open])

  if (!open) return null

  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 z-40 bg-hali-foreground/20"
        onClick={onClose}
        aria-hidden="true"
      />
      {/* Drawer */}
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Navigation menu"
        className="fixed inset-y-0 right-0 z-50 w-72 bg-hali-background shadow-xl flex flex-col"
      >
        <div className="flex items-center justify-between px-6 py-4 border-b border-hali-border">
          <span className="font-display text-xl font-normal text-hali-foreground">
            Hali
          </span>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close menu"
            className="text-hali-muted-foreground hover:text-hali-foreground text-2xl leading-none"
          >
            ×
          </button>
        </div>
        <nav className="flex-1 flex flex-col px-6 py-8 gap-1">
          {NAV_LINKS.map((link) => {
            const isActive = pathname === link.href
            return (
              <Link
                key={link.href}
                href={link.href}
                onClick={onClose}
                className={`py-3 text-base font-medium border-b border-hali-border/60 transition-colors ${
                  isActive
                    ? 'text-hali-primary'
                    : 'text-hali-foreground hover:text-hali-primary'
                }`}
              >
                {link.label}
              </Link>
            )
          })}
        </nav>
        <div className="px-6 py-6 border-t border-hali-border">
          <CtaButton />
        </div>
      </div>
    </>
  )
}

export default function Nav() {
  const pathname = usePathname()
  const [scrolled, setScrolled] = useState(false)
  const [drawerOpen, setDrawerOpen] = useState(false)

  useEffect(() => {
    const handleScroll = () => setScrolled(window.scrollY > 20)
    window.addEventListener('scroll', handleScroll, { passive: true })
    return () => window.removeEventListener('scroll', handleScroll)
  }, [])

  return (
    <>
      <header
        className={`fixed inset-x-0 top-0 z-30 transition-all duration-200 ${
          scrolled
            ? 'bg-hali-background border-b border-hali-border shadow-sm'
            : 'bg-transparent'
        }`}
      >
        <nav
          aria-label="Main navigation"
          className="max-w-6xl mx-auto flex items-center justify-between px-6 py-4"
        >
          {/* Logo / wordmark */}
          <Link
            href="/"
            className="font-display text-2xl font-normal text-hali-foreground"
          >
            Hali
          </Link>

          {/* Desktop links */}
          <ul className="hidden md:flex items-center gap-8" role="list">
            {NAV_LINKS.map((link) => {
              const isActive = pathname === link.href
              return (
                <li key={link.href}>
                  <Link
                    href={link.href}
                    className={`relative text-sm font-medium transition-colors pb-0.5 ${
                      isActive
                        ? 'text-hali-primary after:absolute after:inset-x-0 after:-bottom-0.5 after:h-0.5 after:bg-hali-primary after:rounded-full'
                        : 'text-hali-foreground hover:text-hali-primary'
                    }`}
                  >
                    {link.label}
                  </Link>
                </li>
              )
            })}
          </ul>

          {/* Desktop CTA */}
          <div className="hidden md:block">
            <CtaButton />
          </div>

          {/* Mobile hamburger */}
          <button
            type="button"
            className="md:hidden text-hali-foreground p-1"
            onClick={() => setDrawerOpen(true)}
            aria-label="Toggle menu"
            aria-expanded={drawerOpen}
          >
            <svg
              width="22"
              height="22"
              viewBox="0 0 22 22"
              fill="none"
              aria-hidden="true"
            >
              <rect x="2" y="5" width="18" height="2" rx="1" fill="currentColor" />
              <rect x="2" y="10" width="18" height="2" rx="1" fill="currentColor" />
              <rect x="2" y="15" width="18" height="2" rx="1" fill="currentColor" />
            </svg>
          </button>
        </nav>
      </header>

      <MobileDrawer
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        pathname={pathname}
      />
    </>
  )
}
