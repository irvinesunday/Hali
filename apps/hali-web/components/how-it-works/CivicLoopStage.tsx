'use client'

import { motion } from 'framer-motion'
import type { ReactNode } from 'react'

export interface CivicLoopStageProps {
  stageNumber: number
  title: string
  icon: ReactNode
  explanation: string
  example: string
  isLast?: boolean
}

export default function CivicLoopStage({
  stageNumber,
  title,
  icon,
  explanation,
  example,
  isLast = false,
}: CivicLoopStageProps) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true }}
      transition={{ duration: 0.5 }}
      className={`relative py-16 md:py-20 ${
        isLast ? '' : 'border-b border-hali-border'
      }`}
    >
      <div className="grid md:grid-cols-[30%_70%] gap-8 md:gap-10 items-start">
        {/* Left — decorative number + icon */}
        <div className="relative">
          <span
            aria-hidden="true"
            className="absolute -top-6 md:-top-8 -left-2 font-display text-8xl font-bold text-hali-primary opacity-10 leading-none select-none pointer-events-none"
          >
            {stageNumber}
          </span>
          <div className="relative text-hali-primary w-10 h-10">{icon}</div>
        </div>

        {/* Right — content */}
        <div className="max-w-2xl">
          <h3 className="font-display text-2xl font-bold text-hali-foreground">
            {title}
          </h3>
          <p className="mt-4 text-base leading-relaxed text-hali-foreground">
            {explanation}
          </p>

          <div className="mt-4 bg-hali-surface border-l-4 border-hali-primary rounded-r-lg p-4 pl-5">
            <p className="text-xs font-semibold uppercase tracking-wider text-hali-muted-foreground">
              In practice
            </p>
            <p className="mt-2 text-sm text-hali-foreground italic leading-relaxed">
              {example}
            </p>
          </div>
        </div>
      </div>
    </motion.div>
  )
}
