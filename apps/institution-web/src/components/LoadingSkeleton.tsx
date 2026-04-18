interface LoadingSkeletonProps {
  readonly label: string;
  readonly rowCount?: number;
}

// Neutral loading placeholder used across institution screens while
// queries are in-flight. Uses `role="status"` + an invisible label so
// screen readers announce the pending state; sighted users see the
// shimmer rows. Keeps each surface's empty/loading/error triad
// consistent — see #202 acceptance criteria.
export function LoadingSkeleton({ label, rowCount = 3 }: LoadingSkeletonProps) {
  const rows = Array.from({ length: rowCount });
  return (
    <div role="status" aria-live="polite" className="space-y-3">
      <span className="sr-only">{label}</span>
      {rows.map((_, index) => (
        <div
          key={index}
          aria-hidden="true"
          className="h-14 animate-pulse rounded-md border border-border bg-muted/50"
        />
      ))}
    </div>
  );
}
