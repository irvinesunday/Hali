interface PlaceholderScreenProps {
  readonly title: string;
  readonly description: string;
  readonly landingIssue: string;
}

// Neutral scaffold screen for routes whose real implementation has
// not yet landed. Each screen gets a unique heading and a reference
// to the issue that delivers its content, so reviewers navigating
// the shell immediately know this is intentional placeholder rather
// than a half-finished surface.
export function PlaceholderScreen({ title, description, landingIssue }: PlaceholderScreenProps) {
  return (
    <section className="max-w-2xl space-y-3">
      <h2 className="text-xl font-semibold text-foreground">{title}</h2>
      <p className="text-sm text-muted-foreground">{description}</p>
      <p className="text-xs text-muted-foreground">Implementation lands in {landingIssue}.</p>
    </section>
  );
}
