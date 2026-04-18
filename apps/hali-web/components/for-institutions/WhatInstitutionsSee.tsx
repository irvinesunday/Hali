'use client'

import { motion } from 'framer-motion'

const CARDS: { headline: string; body: React.ReactNode }[] = [
  {
    headline: 'Real-time cluster visibility',
    body: (
      <>
        See what citizens are reporting within your jurisdiction — structured by category, location,
        and severity of confirmation. No noise. No duplicates. One clear picture of what&apos;s
        happening on the ground.
      </>
    ),
  },
  {
    headline: 'Structured response workflow',
    body: (
      <>
        Post live updates, scheduled disruptions, and restoration notices directly against active
        citizen signals. Your response sits alongside the signal — visible to everyone, traceable to
        you.
      </>
    ),
  },
  {
    headline: 'Loop Closure Rate',
    body: (
      <>
        The metric that matters. What percentage of reported civic conditions in your jurisdiction
        actually got resolved? Hali tracks it. Institutions that respond close more loops. That
        record is public.
      </>
    ),
  },
]

export default function WhatInstitutionsSee() {
  return (
    <section className="bg-hali-muted py-20 md:py-28 px-6">
      <div className="max-w-6xl mx-auto">
        <h2 className="font-display text-3xl md:text-4xl text-hali-foreground text-center">
          What institutions see
        </h2>
        <div className="mt-12 grid gap-6 md:grid-cols-3 md:gap-8">
          {CARDS.map((card, i) => (
            <motion.div
              key={card.headline}
              initial={{ opacity: 0, y: 16 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ delay: i * 0.1 }}
              className="bg-hali-card rounded-xl border border-hali-border p-6 transition-shadow hover:shadow-md"
            >
              <h3 className="text-lg font-semibold text-hali-foreground">{card.headline}</h3>
              <p className="mt-3 text-base leading-relaxed text-hali-muted-foreground">
                {card.body}
              </p>
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  )
}
