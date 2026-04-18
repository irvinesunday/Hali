'use client'

import { motion } from 'framer-motion'

export default function ResolutionExplained() {
  return (
    <section className="bg-hali-background py-16 md:py-24 px-6">
      <motion.div
        initial={{ opacity: 0, y: 16 }}
        whileInView={{ opacity: 1, y: 0 }}
        viewport={{ once: true }}
        transition={{ duration: 0.6 }}
        className="max-w-2xl mx-auto bg-hali-surface rounded-2xl p-10 md:p-16"
      >
        <h2 className="font-display text-3xl font-bold text-hali-foreground">
          Resolution belongs to the people affected
        </h2>
        <div className="mt-8 space-y-6 text-base md:text-lg leading-relaxed text-hali-foreground">
          <p>
            Most civic systems declare problems solved when institutions say they&apos;re solved. Hali doesn&apos;t work that way.
          </p>
          <p>
            When a utility or authority posts a restoration notice, Hali reads it as a proposal. It asks the people who were affected — those who reported the issue, who said they were impacted — whether the condition has actually improved for them.
          </p>
          <p>
            Only when enough of them confirm does the cluster resolve. If they don&apos;t confirm — or if new affected reports arrive — the signal stays active, regardless of what the official update says.
          </p>
          <p>
            This asymmetry is structural. It&apos;s not a feature. It&apos;s the foundation of why Hali can be trusted by both sides.
          </p>
        </div>
      </motion.div>
    </section>
  )
}
