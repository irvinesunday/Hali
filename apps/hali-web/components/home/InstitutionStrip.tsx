import Link from 'next/link'

export default function InstitutionStrip() {
  return (
    <section className="bg-hali-foreground text-hali-background py-20">
      <div className="max-w-6xl mx-auto px-6">
        <div className="md:grid md:grid-cols-[1fr_auto] md:items-center md:gap-10 space-y-6 md:space-y-0">
          <div>
            <h2 className="font-display text-3xl md:text-4xl">
              Are you a public institution or utility?
            </h2>
            <p className="mt-3 text-lg text-hali-background/80 max-w-2xl">
              Hali gives institutions a structured view of real conditions on the ground — and a direct channel to respond. No more guessing what&apos;s happening on the ground.
            </p>
          </div>
          <div>
            <Link
              href="/for-institutions"
              className="inline-flex items-center gap-1 text-lg font-medium hover:underline"
            >
              Become a pilot partner →
            </Link>
          </div>
        </div>
      </div>
    </section>
  )
}
