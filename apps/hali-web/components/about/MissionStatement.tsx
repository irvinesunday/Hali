'use client'

import { motion } from 'framer-motion'

export default function MissionStatement() {
  return (
    <section className="bg-hali-background py-16 md:py-20 px-6">
      <motion.div
        initial={{ opacity: 0, y: 16 }}
        whileInView={{ opacity: 1, y: 0 }}
        viewport={{ once: true }}
        transition={{ duration: 0.5 }}
        className="max-w-2xl mx-auto"
      >
        <h2 className="font-display text-2xl md:text-3xl font-bold text-hali-foreground mb-8">
          Why Hali exists
        </h2>
        <p className="text-base md:text-lg text-hali-foreground leading-relaxed md:leading-loose mb-6">
          In most cities, when something goes wrong — a road floods, the power goes out, water stops running — there is no reliable way to know whether it&apos;s just you, your street, or your entire area. People turn to WhatsApp groups, call hotlines that go unanswered, or post to social media hoping someone official sees it. The information exists. It&apos;s scattered, unstructured, and invisible to the systems that could act on it.
        </p>
        <p className="text-base md:text-lg text-hali-foreground leading-relaxed md:leading-loose">
          Hali is built to close that gap. It turns individual observations into structured, visible civic conditions — and gives institutions a clear, real-time picture of what is actually happening on the ground. When a problem is visible and confirmed, response follows. When a response arrives, citizens see it. When the condition improves, the people affected say so. That feedback loop is the product.
        </p>
      </motion.div>
    </section>
  )
}
