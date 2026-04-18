'use client'

import { motion } from 'framer-motion'
import WaveIcon from '@/components/icons/WaveIcon'
import PeopleIcon from '@/components/icons/PeopleIcon'
import BuildingIcon from '@/components/icons/BuildingIcon'

const CARDS = [
  {
    icon: WaveIcon,
    title: 'See what\u2019s happening',
    body: 'Know what\u2019s affecting your area \u2014 roads, water, power \u2014 in real time.',
  },
  {
    icon: PeopleIcon,
    title: 'Add your voice',
    body: 'Tap once to confirm you\u2019re affected. Your signal joins others nearby.',
  },
  {
    icon: BuildingIcon,
    title: 'See who\u2019s responding',
    body: 'Watch institutions acknowledge and respond to real conditions.',
  },
]

export default function ValuePropositionSection() {
  return (
    <section className="bg-hali-background py-20 md:py-28">
      <div className="max-w-6xl mx-auto px-6">
        <h2 className="font-display text-3xl md:text-5xl text-hali-foreground">
          A clearer picture of your area
        </h2>
        <p className="mt-4 text-lg text-hali-muted-foreground max-w-2xl">
          Hali shows what&apos;s happening around you — and how it&apos;s being resolved.
        </p>

        <div className="mt-12 grid gap-6 md:grid-cols-3">
          {CARDS.map((card, i) => {
            const Icon = card.icon
            return (
              <motion.div
                key={card.title}
                initial={{ opacity: 0, y: 16 }}
                whileInView={{ opacity: 1, y: 0 }}
                viewport={{ once: true }}
                transition={{ delay: i * 0.1 }}
                className="bg-hali-card rounded-xl border border-hali-border p-6 hover:shadow-md transition-shadow"
              >
                <Icon className="w-8 h-8 text-hali-primary" />
                <h3 className="mt-5 font-display text-xl text-hali-foreground">
                  {card.title}
                </h3>
                <p className="mt-2 text-hali-foreground/75 leading-relaxed">
                  {card.body}
                </p>
              </motion.div>
            )
          })}
        </div>
      </div>
    </section>
  )
}
