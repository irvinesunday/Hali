'use client'

import Image from 'next/image'
import { motion } from 'framer-motion'

export default function FoundingStory() {
  // If set to an external domain, add it to next.config.mjs images.remotePatterns.
  const photoSrc = process.env.NEXT_PUBLIC_FOUNDER_PHOTO_URL || null

  return (
    <section className="bg-hali-background py-16 md:py-20 px-6">
      <motion.div
        initial={{ opacity: 0, y: 16 }}
        whileInView={{ opacity: 1, y: 0 }}
        viewport={{ once: true }}
        transition={{ duration: 0.5 }}
        className="max-w-4xl mx-auto grid md:grid-cols-[40%_60%] gap-10 md:gap-12 items-start"
      >
        {/* Photo */}
        <div className="flex justify-center md:justify-start">
          {photoSrc ? (
            <Image
              src={photoSrc}
              alt="Founder of Hali"
              width={192}
              height={192}
              className="w-48 h-48 rounded-full object-cover border-2 border-hali-border"
            />
          ) : (
            <div
              aria-hidden="true"
              className="w-48 h-48 rounded-full bg-hali-surface border-2 border-hali-border"
            />
          )}
        </div>

        {/* Bio */}
        <div>
          {/* PLACEHOLDER — replace before launch */}
          <h2 className="font-display text-2xl md:text-3xl font-bold text-hali-foreground">
            [Founder Name]
          </h2>
          <p className="mt-2 text-sm text-hali-muted-foreground uppercase tracking-wider font-semibold">
            Founder, Hali
          </p>
          {/* PLACEHOLDER — replace before launch */}
          <p className="mt-6 text-base md:text-lg text-hali-foreground leading-relaxed">
            [2–3 sentences. Why this was built, what you saw, why you believed it was worth building. Plain language. No jargon. No LinkedIn summary tone.]
          </p>
          {/* PLACEHOLDER — replace before launch */}
          <p className="mt-4 text-base text-hali-muted-foreground leading-relaxed">
            [One sentence on background if relevant — keep it human, not résumé.]
          </p>
        </div>
      </motion.div>
    </section>
  )
}
