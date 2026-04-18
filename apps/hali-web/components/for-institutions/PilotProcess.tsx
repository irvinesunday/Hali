'use client'

import { motion } from 'framer-motion'

const STEPS = [
  {
    n: 1,
    title: 'Submit an inquiry',
    body: 'Fill in the form below. Tell us your organisation, your area of operation, and which civic categories matter most to you.',
  },
  {
    n: 2,
    title: 'Scoping call',
    body: "We'll reach out within 48 hours to understand your jurisdiction, your existing workflows, and how Hali fits into them.",
  },
  {
    n: 3,
    title: 'Pilot onboarding',
    body: "We configure Hali for your jurisdiction and categories, connect you to the institution dashboard, and brief your team. You're live within days.",
  },
]

export default function PilotProcess() {
  return (
    <section className="bg-hali-background py-20 md:py-28 px-6">
      <div className="max-w-6xl mx-auto">
        <h2 className="font-display text-3xl md:text-4xl text-hali-foreground text-center">
          How pilot partnership works
        </h2>
        <div className="mt-12 grid gap-10 md:grid-cols-3 md:gap-8">
          {STEPS.map((step, i) => (
            <motion.div
              key={step.n}
              initial={{ opacity: 0, y: 16 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ delay: i * 0.1 }}
              className="md:border-r md:border-dashed md:border-hali-border md:pr-8 md:last:border-r-0 md:last:pr-0"
            >
              <span className="font-display text-5xl text-hali-primary/15 leading-none block">
                {step.n}
              </span>
              <h3 className="mt-3 font-display text-xl text-hali-foreground">{step.title}</h3>
              <p className="mt-2 text-base leading-relaxed text-hali-muted-foreground">
                {step.body}
              </p>
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  )
}
