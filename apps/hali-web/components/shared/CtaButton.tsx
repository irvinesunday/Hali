'use client'

import { useState } from 'react'
import EmailCaptureModal from './EmailCaptureModal'

interface CtaButtonProps {
  variant?: 'primary' | 'secondary' | 'outline'
  size?: 'default' | 'large'
  className?: string
}

export default function CtaButton({ variant = 'primary', size = 'default', className = '' }: CtaButtonProps) {
  const [modalOpen, setModalOpen] = useState(false)
  const isLive = process.env.NEXT_PUBLIC_APP_LAUNCH_STATE === 'live'
  const appStore = process.env.NEXT_PUBLIC_APP_STORE_URL
  const playStore = process.env.NEXT_PUBLIC_PLAY_STORE_URL

  const label = isLive ? 'Open Hali' : 'Get notified at launch'

  const base = 'inline-flex items-center justify-center rounded-full font-medium transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2'
  const sizeClass = size === 'large' ? 'text-base px-7 py-3.5' : 'text-sm px-5 py-2.5'
  const variantClass = {
    primary: 'bg-hali-primary text-hali-primary-foreground hover:opacity-90 focus-visible:ring-hali-primary',
    secondary: 'bg-hali-background text-hali-primary border border-hali-primary hover:bg-hali-accent focus-visible:ring-hali-primary',
    outline: 'bg-transparent text-hali-background border border-hali-background/40 hover:bg-hali-background/10 focus-visible:ring-hali-background',
  }[variant]

  // Live state with one or both store URLs
  if (isLive && (appStore || playStore)) {
    if (appStore && playStore) {
      return (
        <div className={`flex flex-wrap gap-3 ${className}`}>
          <a href={appStore} className={`${base} ${sizeClass} ${variantClass}`}>Download on App Store</a>
          <a href={playStore} className={`${base} ${sizeClass} ${variantClass}`}>Get it on Google Play</a>
        </div>
      )
    }
    const url = appStore || playStore
    return (
      <a href={url} className={`${base} ${sizeClass} ${variantClass} ${className}`}>{label}</a>
    )
  }

  // Prelaunch — open modal
  return (
    <>
      <button type="button" onClick={() => setModalOpen(true)} className={`${base} ${sizeClass} ${variantClass} ${className}`}>
        {label}
      </button>
      <EmailCaptureModal open={modalOpen} onClose={() => setModalOpen(false)} />
    </>
  )
}
