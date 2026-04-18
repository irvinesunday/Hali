import CtaButton from '@/components/shared/CtaButton'

export default function AboutCtaSection() {
  return (
    <section className="bg-hali-background py-20 px-6">
      <div className="max-w-3xl mx-auto text-center">
        <h2 className="font-display text-3xl md:text-4xl text-hali-foreground">
          See what&apos;s happening in your area
        </h2>
        <div className="mt-8 flex justify-center">
          <CtaButton variant="primary" size="large" />
        </div>
      </div>
    </section>
  )
}
