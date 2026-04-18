'use client'

import Image from 'next/image'
import { motion } from 'framer-motion'
import CtaButton from '@/components/shared/CtaButton'

export default function HeroSection() {
  return (
    <section className="relative pt-24 md:pt-28 py-20 md:py-32 overflow-hidden bg-hali-background before:absolute before:inset-0 before:bg-[radial-gradient(ellipse_at_top_right,_oklch(0.92_0.02_180)_0%,_transparent_60%)] before:opacity-50 before:pointer-events-none">
      <div className="max-w-6xl mx-auto px-6 relative">
        <div className="grid md:grid-cols-[55%_45%] gap-12 items-center">
          {/* Left column */}
          <motion.div
            initial={{ opacity: 0, y: 16 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.5 }}
          >
            <h1 className="font-display text-4xl md:text-6xl leading-[1.05] text-hali-foreground">
              What&apos;s happening in your area?
            </h1>
            <p className="mt-6 text-lg md:text-xl text-hali-foreground/80 leading-relaxed max-w-xl">
              Know what&apos;s affecting your area — roads, water, power — as it happens. Hali brings together what residents are experiencing and what authorities are doing about it.
            </p>
            <p className="mt-6 font-display text-base text-hali-muted-foreground italic">
              What&apos;s the Hali?
            </p>
            <div className="mt-10 flex flex-wrap items-center gap-5">
              <CtaButton variant="primary" size="large" />
              <a
                href="#how-it-works"
                className="text-hali-primary hover:underline font-medium"
              >
                See how it works →
              </a>
            </div>
          </motion.div>

          {/* Right column — phone mockup */}
          <motion.div
            initial={{ opacity: 0, y: 16 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.2, duration: 0.6 }}
          >
            <div className="relative mx-auto max-w-[320px] md:max-w-[360px] rotate-1 rounded-3xl ring-[6px] ring-hali-accent shadow-2xl overflow-hidden">
              <Image
                src="/screenshots/screenshot-home-feed.png"
                alt="Hali home feed showing active civic conditions in a neighbourhood"
                width={390}
                height={844}
                priority
                className="w-full h-auto"
              />
            </div>
          </motion.div>
        </div>
      </div>
    </section>
  )
}
