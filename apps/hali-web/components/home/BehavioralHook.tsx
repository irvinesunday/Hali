import CtaButton from '@/components/shared/CtaButton'

export default function BehavioralHook() {
  return (
    <section className="bg-hali-secondary py-20 md:py-28">
      <div className="max-w-3xl mx-auto px-6 text-center">
        <h2 className="font-display text-3xl md:text-5xl text-hali-foreground">
          Stay ahead of what&apos;s happening
        </h2>
        <p className="mt-4 text-lg text-hali-foreground/75 max-w-xl mx-auto">
          Road closures. Water outages. Power issues. Know before it affects you.
        </p>
        <div className="mt-10 flex justify-center">
          <CtaButton variant="primary" size="large" />
        </div>
      </div>
    </section>
  )
}
