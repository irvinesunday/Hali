interface EmptyStateProps {
  readonly title: string;
  readonly description: string;
}

// Neutral "nothing here" surface. Used when an institution has zero
// active signals, zero areas, etc. The copy is deliberately calm and
// factual — the platform's product doctrine keeps the UI out of
// interpretive territory ("all quiet" is not a judgement, it's the
// literal state).
export function EmptyState({ title, description }: EmptyStateProps) {
  return (
    <section className="max-w-xl space-y-2 rounded-md border border-dashed border-border bg-background p-6">
      <h2 className="text-base font-semibold text-foreground">{title}</h2>
      <p className="text-sm text-muted-foreground">{description}</p>
    </section>
  );
}
