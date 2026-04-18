'use client'

import { motion } from 'framer-motion'

const DOCTRINE = [
  {
    headline: 'Neutral by design',
    body: 'Hali surfaces conditions, not opinions. There are no rankings, no blame attribution, no political interpretation. A pothole is a pothole. A power outage is a power outage. What citizens report and what institutions respond with are both shown — side by side, without editorial interference.',
  },
  {
    headline: 'Dual visibility',
    body: "Citizen signals and official institutional responses are always visible together. One never replaces or overrides the other. If an institution's response contradicts what citizens are still experiencing, both remain visible. Contradiction is information — Hali does not resolve it for you.",
  },
  {
    headline: 'Resolution belongs to citizens',
    body: 'A condition is resolved when the people who were affected say it is. Institutional declarations of restoration are treated as proposals, not facts. This is not a technicality — it is the mechanism that makes the system credible to both sides.',
  },
]

export default function PlatformDoctrine() {
  return (
    <section className="bg-hali-surface py-16 md:py-20 px-6">
      <div className="max-w-4xl mx-auto">
        <motion.h2
          initial={{ opacity: 0, y: 16 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ duration: 0.5 }}
          className="font-display text-2xl md:text-3xl font-bold text-hali-foreground mb-10"
        >
          How Hali is built to behave
        </motion.h2>

        <div>
          {DOCTRINE.map((item, i) => (
            <motion.div
              key={item.headline}
              initial={{ opacity: 0, y: 16 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ duration: 0.5, delay: i * 0.08 }}
              className={`flex items-start gap-6 py-8 ${
                i < DOCTRINE.length - 1 ? 'border-b border-hali-border' : ''
              }`}
            >
              <div
                aria-hidden="true"
                className="shrink-0 w-1 h-10 bg-hali-primary rounded-full mt-1"
              />
              <div>
                <h3 className="text-xl font-semibold text-hali-foreground">
                  {item.headline}
                </h3>
                <p className="mt-3 text-base text-hali-muted-foreground leading-relaxed">
                  {item.body}
                </p>
              </div>
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  )
}
