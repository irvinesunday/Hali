'use client'

import { motion } from 'framer-motion'

export default function AboutHero() {
  return (
    <section className="bg-hali-background py-24 md:py-32 px-6">
      <div className="max-w-3xl mx-auto text-center">
        <motion.h1
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.5 }}
          className="font-display text-4xl md:text-5xl font-bold text-hali-foreground leading-[1.1]"
        >
          Civic infrastructure for real-world cities
        </motion.h1>
        <motion.p
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.5, delay: 0.12 }}
          className="text-xl text-hali-muted-foreground mt-6 leading-relaxed"
        >
          Hali is built on a simple premise: when people can see what&apos;s happening around them — and see that something is being done about it — cities work better.
        </motion.p>
      </div>
    </section>
  )
}
