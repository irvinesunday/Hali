import ForInstitutionsHero from '@/components/for-institutions/ForInstitutionsHero'
import WhatInstitutionsSee from '@/components/for-institutions/WhatInstitutionsSee'
import PilotProcess from '@/components/for-institutions/PilotProcess'
import PilotInquiryForm from '@/components/for-institutions/PilotInquiryForm'

export const metadata = {
  title: 'For Institutions — Become a Hali pilot partner',
  description:
    'Give your institution a structured view of real conditions on the ground and a direct channel to respond. Become a Hali pilot partner.',
  openGraph: {
    title: 'For Institutions — Become a Hali pilot partner',
    description:
      'Give your institution a structured view of real conditions on the ground and a direct channel to respond. Become a Hali pilot partner.',
    url: 'https://gethali.app/for-institutions',
    images: [{ url: '/og-image.png', width: 1200, height: 630 }],
  },
}

export default function ForInstitutionsPage() {
  return (
    <main>
      <ForInstitutionsHero />
      <WhatInstitutionsSee />
      <PilotProcess />
      <PilotInquiryForm />
    </main>
  )
}
