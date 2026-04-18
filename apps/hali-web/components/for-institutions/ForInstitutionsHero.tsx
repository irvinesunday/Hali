'use client'

import { motion } from 'framer-motion'

export default function ForInstitutionsHero() {
  return (
    <section className="bg-hali-background py-24 md:py-32 px-6">
      <div className="max-w-3xl mx-auto text-center">
        <motion.div
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.5, delay: 0 }}
        >
          <h1 className="font-display text-4xl md:text-5xl font-bold text-hali-foreground">
            Real conditions. Structured response.
          </h1>
        </motion.div>
        <motion.div
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.5, delay: 0.15 }}
        >
          <p className="mt-6 text-xl leading-relaxed text-hali-muted-foreground">
            Hali gives institutions a structured view of what&apos;s happening on the ground — and a direct, public channel to respond. No more guessing. No more fragmented reports.
          </p>
        </motion.div>
      </div>
    </section>
  )
}
