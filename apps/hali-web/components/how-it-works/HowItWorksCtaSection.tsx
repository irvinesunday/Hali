import CtaButton from '@/components/shared/CtaButton'

export default function HowItWorksCtaSection() {
  return (
    <section className="bg-hali-background py-20 px-6">
      <div className="max-w-3xl mx-auto text-center">
        <h2 className="font-display text-3xl md:text-4xl text-hali-foreground">
          Ready to see what&apos;s happening in your area?
        </h2>
        <p className="mt-4 text-lg text-hali-muted-foreground">
          Open Hali and check what&apos;s active near you right now.
        </p>
        <div className="mt-8 flex justify-center">
          <CtaButton variant="primary" size="large" />
        </div>
      </div>
    </section>
  )
}
