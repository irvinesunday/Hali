'use client'

import { motion } from 'framer-motion'

export default function HowItWorksHero() {
  return (
    <section className="bg-hali-background py-24 md:py-32 px-6">
      <div className="max-w-3xl mx-auto text-center">
        <motion.h1
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.5 }}
          className="font-display text-4xl md:text-5xl text-hali-foreground leading-[1.1]"
        >
          The civic feedback loop
        </motion.h1>
        <motion.p
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.5, delay: 0.12 }}
          className="text-xl text-hali-muted-foreground max-w-2xl mx-auto mt-6 leading-relaxed"
        >
          Hali is not a complaint form or a hotline. It&apos;s a system that turns what residents are experiencing into a structured, visible civic reality — and connects that reality to the institutions that can act on it.
        </motion.p>
      </div>
    </section>
  )
}
