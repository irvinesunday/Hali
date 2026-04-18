interface ErrorStateProps {
  readonly title: string;
  readonly description: string;
  readonly onRetry?: () => void;
}

// Surface-level error view. The RootErrorBoundary catches render-time
// throws; this component renders when a query *returns* an error state
// (server 5xx, network drop, etc.) without tearing down the rest of
// the shell. Keeps the user inside the dashboard and offers a
// deliberate retry affordance rather than a full-page crash.
export function ErrorState({ title, description, onRetry }: ErrorStateProps) {
  return (
    <section
      role="alert"
      className="max-w-xl space-y-3 rounded-md border border-border bg-card p-6"
    >
      <h2 className="text-base font-semibold text-foreground">{title}</h2>
      <p className="text-sm text-muted-foreground">{description}</p>
      {onRetry ? (
        <button
          type="button"
          onClick={onRetry}
          className="rounded-md border border-border bg-background px-3 py-1.5 text-sm font-medium text-foreground hover:bg-muted"
        >
          Retry
        </button>
      ) : null}
    </section>
  );
}
