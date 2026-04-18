import HeroSection from '@/components/home/HeroSection'
import ValuePropositionSection from '@/components/home/ValuePropositionSection'
import CivicLoopSection from '@/components/home/CivicLoopSection'
import ScreenshotsSection from '@/components/home/ScreenshotsSection'
import InstitutionStrip from '@/components/home/InstitutionStrip'
import BehavioralHook from '@/components/home/BehavioralHook'

export const metadata = {
  title: "Hali — What's happening in your area",
  description: "Know what's affecting your area — roads, water, power — as it happens.",
  openGraph: {
    title: "Hali — What's happening in your area",
    description: "Know what's affecting your area — roads, water, power — as it happens.",
    url: 'https://gethali.app',
    images: [{ url: '/og-image.png', width: 1200, height: 630 }],
  },
}

export default function HomePage() {
  return (
    <main>
      <HeroSection />
      <ValuePropositionSection />
      <CivicLoopSection />
      <ScreenshotsSection />
      <InstitutionStrip />
      <BehavioralHook />
    </main>
  )
}
