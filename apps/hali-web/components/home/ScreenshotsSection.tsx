'use client'

import Image from 'next/image'
import { motion } from 'framer-motion'

const SCREENSHOTS = [
  {
    src: '/screenshots/screenshot-home-feed.png',
    alt: 'Hali home feed',
    caption: 'See active conditions in your area',
  },
  {
    src: '/screenshots/screenshot-cluster-detail.png',
    alt: 'Hali signal detail',
    caption: 'Understand what\u2019s happening and why',
  },
  {
    src: '/screenshots/screenshot-composer.png',
    alt: 'Hali report composer',
    caption: 'Report in seconds, in plain language',
  },
]

export default function ScreenshotsSection() {
  return (
    <section className="bg-hali-background py-20 md:py-28">
      <div className="max-w-6xl mx-auto px-6">
        <h2 className="font-display text-3xl md:text-5xl text-hali-foreground text-center">
          Built for how your city actually works
        </h2>

        {/* Mobile: horizontal scroll. Desktop: 3-col grid */}
        <div className="mt-12 flex overflow-x-auto snap-x snap-mandatory gap-5 -mx-6 px-6 md:grid md:grid-cols-3 md:gap-8 md:items-end md:mx-0 md:px-0 md:overflow-visible">
          {SCREENSHOTS.map((shot, i) => (
            <motion.div
              key={shot.src}
              initial={{ opacity: 0, y: 16 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ delay: i * 0.1 }}
              className={`snap-center min-w-[260px] md:min-w-0 md:w-auto flex-shrink-0 ${i === 1 ? 'md:scale-105' : ''}`}
            >
              <div className="rounded-3xl ring-4 ring-hali-border shadow-xl overflow-hidden">
                <Image
                  src={shot.src}
                  alt={shot.alt}
                  width={390}
                  height={844}
                  className="w-full h-auto"
                />
              </div>
              <p className="mt-5 text-center text-sm text-hali-muted-foreground">
                {shot.caption}
              </p>
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  )
}
