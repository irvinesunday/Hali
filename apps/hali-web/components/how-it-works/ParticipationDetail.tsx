'use client'

import { motion } from 'framer-motion'

export default function ParticipationDetail() {
  return (
    <section className="bg-hali-background py-20 md:py-24">
      <div className="max-w-5xl mx-auto px-6">
        <motion.h2
          initial={{ opacity: 0, y: 16 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ duration: 0.5 }}
          className="font-display text-3xl md:text-4xl font-bold text-hali-foreground"
        >
          How participation works
        </motion.h2>

        <div className="mt-10 grid md:grid-cols-2 gap-6">
          {/* I'm Affected */}
          <motion.div
            initial={{ opacity: 0, y: 16 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.5 }}
            className="bg-hali-surface rounded-xl border border-hali-border border-l-4 border-l-hali-primary p-6"
          >
            <h3 className="text-lg font-semibold text-hali-foreground">
              I&apos;m Affected
            </h3>
            <p className="mt-3 text-base text-hali-muted-foreground leading-relaxed">
              You&apos;re directly experiencing the issue. Your participation carries weight in confirming the condition — and in confirming its resolution. When the system asks whether things have improved, your answer matters most.
            </p>
          </motion.div>

          {/* I'm Observing */}
          <motion.div
            initial={{ opacity: 0, y: 16 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.5, delay: 0.08 }}
            className="bg-hali-surface rounded-xl border border-hali-border p-6"
          >
            <h3 className="text-lg font-semibold text-hali-foreground">
              I&apos;m Observing
            </h3>
            <p className="mt-3 text-base text-hali-muted-foreground leading-relaxed">
              You&apos;ve seen or heard about the issue but aren&apos;t directly experiencing it. Your signal adds to the visible picture without claiming direct impact. Both matter. Both count toward the cluster.
            </p>
          </motion.div>
        </div>

        {/* Add Further Context */}
        <motion.div
          initial={{ opacity: 0, y: 16 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ duration: 0.5 }}
          className="mt-8 bg-hali-surface rounded-xl p-6"
        >
          <h3 className="text-lg font-semibold text-hali-foreground">
            Add Further Context
          </h3>
          <p className="mt-3 text-base text-hali-muted-foreground leading-relaxed">
            After marking yourself as affected, you have a short window to add specific detail — &ldquo;water pressure dropped completely around 6am&rdquo; or &ldquo;the road is impassable for motorcycles but cars can still squeeze through.&rdquo; This context helps institutions understand the condition without creating a discussion thread.
          </p>
        </motion.div>

        {/* Anonymity statement */}
        <div className="mt-16 pt-10 border-t border-hali-border">
          <p className="text-sm text-hali-muted-foreground italic text-center max-w-2xl mx-auto leading-relaxed">
            &ldquo;Your name is never shown. What&apos;s visible is the pattern — how many people, where, and for how long.&rdquo;
          </p>
        </div>
      </div>
    </section>
  )
}
