'use client'

import Link from 'next/link'
import { motion } from 'framer-motion'

const STEPS = [
  {
    n: 1,
    title: 'Report',
    body: 'Type what you see. Hali understands it.',
  },
  {
    n: 2,
    title: 'Cluster',
    body: 'Your signal joins others nearby. A real condition emerges.',
  },
  {
    n: 3,
    title: 'Response',
    body: 'Institutions see the same picture and respond publicly.',
  },
  {
    n: 4,
    title: 'Resolution',
    body: 'When things improve, you confirm it. Resolution is verified, not declared.',
  },
]

export default function CivicLoopSection() {
  return (
    <section id="how-it-works" className="bg-hali-accent/40 py-20 md:py-28">
      <div className="max-w-6xl mx-auto px-6">
        <h2 className="font-display text-3xl md:text-5xl text-hali-foreground">
          How Hali works in real life
        </h2>
        <p className="mt-4 text-lg text-hali-muted-foreground max-w-2xl">
          When something happens, you&apos;re not the only one seeing it. Hali turns individual reports into a shared, visible reality.
        </p>

        <div className="mt-12 grid gap-10 md:grid-cols-4">
          {STEPS.map((step, i) => (
            <motion.div
              key={step.n}
              initial={{ opacity: 0, y: 16 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ delay: i * 0.1 }}
            >
              <span className="font-display text-7xl text-hali-primary/15 leading-none block">
                {step.n}
              </span>
              <h3 className="mt-2 font-display text-xl text-hali-foreground">
                {step.title}
              </h3>
              <p className="mt-2 text-hali-foreground/75">
                {step.body}
              </p>
            </motion.div>
          ))}
        </div>

        <div className="mt-14">
          <Link
            href="/how-it-works"
            className="text-hali-primary hover:underline font-medium"
          >
            Learn more about how it works →
          </Link>
        </div>
      </div>
    </section>
  )
}
