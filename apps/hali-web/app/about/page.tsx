import AboutHero from '@/components/about/AboutHero'
import MissionStatement from '@/components/about/MissionStatement'
import PlatformDoctrine from '@/components/about/PlatformDoctrine'
import FoundingStory from '@/components/about/FoundingStory'
import AboutCtaSection from '@/components/about/AboutCtaSection'

export const metadata = {
  title: 'About Hali — Civic infrastructure for real-world cities',
  description:
    'Hali turns individual observations into structured, visible civic conditions — and connects that reality to the institutions that can act on it.',
  openGraph: {
    title: 'About Hali — Civic infrastructure for real-world cities',
    description:
      'Hali turns individual observations into structured, visible civic conditions — and connects that reality to the institutions that can act on it.',
    url: 'https://gethali.app/about',
    images: [{ url: '/og-image.png', width: 1200, height: 630 }],
  },
}

export default function AboutPage() {
  return (
    <main id="main">
      <AboutHero />
      <MissionStatement />
      <PlatformDoctrine />
      <FoundingStory />
      <AboutCtaSection />
    </main>
  )
}
