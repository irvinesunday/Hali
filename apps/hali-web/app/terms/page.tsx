export const metadata = {
  title: 'Terms of Use',
}

export default function TermsPage() {
  return (
    <main className="max-w-3xl mx-auto px-6 py-20">
      <h1 className="font-display text-3xl font-bold text-hali-foreground mb-4">
        Terms of Use
      </h1>
      <p className="text-sm text-hali-muted-foreground mb-12">
        {/* PLACEHOLDER — insert date before launch */}
        Last updated: April 2026
      </p>

      <section>
        <h2 className="text-xl font-semibold text-hali-foreground mt-10 mb-4">
          Overview
        </h2>
        <p className="text-base text-hali-foreground leading-relaxed">
          {/* PLACEHOLDER — legal review pending. Replace with final terms copy. */}
          These terms govern your use of the Hali platform. By using Hali, you agree to these terms. This document is pending legal review and will be updated before public launch.
        </p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-hali-foreground mt-10 mb-4">
          Use of Service
        </h2>
        <p className="text-base text-hali-foreground leading-relaxed">
          {/* PLACEHOLDER — to be completed by legal review */}
          Hali is a civic information platform. You may use it to report civic conditions, participate in existing signals, and receive information about your area. You agree not to submit false, misleading, or abusive content.
        </p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-hali-foreground mt-10 mb-4">
          Prohibited Uses
        </h2>
        <p className="text-base text-hali-foreground leading-relaxed">
          {/* PLACEHOLDER — to be completed by legal review */}
          You may not use Hali to submit coordinated false signals, harass other users, circumvent platform integrity systems, or use the platform for commercial purposes without authorisation.
        </p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-hali-foreground mt-10 mb-4">
          Disclaimers
        </h2>
        <p className="text-base text-hali-foreground leading-relaxed">
          {/* PLACEHOLDER — to be completed by legal review */}
          Hali aggregates and displays civic signals as reported. We do not verify the accuracy of individual reports and are not liable for actions taken based on platform information.
        </p>
      </section>

      <section>
        <h2 className="text-xl font-semibold text-hali-foreground mt-10 mb-4">
          Contact
        </h2>
        <p className="text-base text-hali-foreground leading-relaxed">
          {/* PLACEHOLDER — insert contact details before launch */}
          For terms-related questions, contact us at legal@gethali.app
        </p>
      </section>
    </main>
  )
}
