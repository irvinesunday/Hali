import HowItWorksHero from '@/components/how-it-works/HowItWorksHero'
import ExpandedCivicLoop from '@/components/how-it-works/ExpandedCivicLoop'
import ParticipationDetail from '@/components/how-it-works/ParticipationDetail'
import ResolutionExplained from '@/components/how-it-works/ResolutionExplained'
import HowItWorksCtaSection from '@/components/how-it-works/HowItWorksCtaSection'

export const metadata = {
  title: 'How Hali works — The civic feedback loop',
  description:
    'Understand how citizen signals become visible civic conditions, how institutions respond, and how resolution is confirmed — not declared.',
  openGraph: {
    title: 'How Hali works — The civic feedback loop',
    description:
      'Understand how citizen signals become visible civic conditions, how institutions respond, and how resolution is confirmed — not declared.',
    url: 'https://gethali.app/how-it-works',
    images: [{ url: '/og-image.png', width: 1200, height: 630 }],
  },
}

export default function HowItWorksPage() {
  return (
    <main>
      <HowItWorksHero />
      <ExpandedCivicLoop />
      <ParticipationDetail />
      <ResolutionExplained />
      <HowItWorksCtaSection />
    </main>
  )
}
