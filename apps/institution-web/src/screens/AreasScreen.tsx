import { useQuery } from "@tanstack/react-query";
import { getInstitutionAreas } from "../api/institution";
import { EmptyState } from "../components/EmptyState";
import { ErrorState } from "../components/ErrorState";
import { LoadingSkeleton } from "../components/LoadingSkeleton";
import { institutionKeys } from "../query/keys";

// Full areas list for an institution. The Overview page caps its
// strip at 6 top areas; this screen shows the complete jurisdiction
// set so operators can audit coverage.
export function AreasScreen() {
  const areas = useQuery({
    queryKey: institutionKeys.areas(),
    queryFn: getInstitutionAreas,
  });

  if (areas.isLoading) {
    return <LoadingSkeleton label="Loading areas" rowCount={4} />;
  }

  if (areas.isError || !areas.data) {
    return (
      <ErrorState
        title="We couldn't load your areas."
        description="The areas service is not responding. Check your connection and retry."
        onRetry={() => {
          void areas.refetch();
        }}
      />
    );
  }

  if (areas.data.items.length === 0) {
    return (
      <EmptyState
        title="No areas assigned"
        description="Your institution has no jurisdictions configured. Ask a Hali admin to attach the wards you cover."
      />
    );
  }

  return (
    <section aria-label="Areas" className="space-y-3">
      <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {areas.data.items.map((area) => (
          <li
            key={area.id}
            data-testid="area-item"
            className="space-y-1 rounded-md border border-border bg-card p-4"
          >
            <p className="text-sm font-semibold text-foreground">{area.name}</p>
            <p className="text-xs text-muted-foreground">
              Condition: <span className="font-medium text-foreground">{area.condition}</span>
            </p>
            <p className="text-xs text-muted-foreground">{area.activeSignals} active signals</p>
            {area.topCategory ? (
              <p className="text-xs text-muted-foreground">Top category: {area.topCategory}</p>
            ) : null}
          </li>
        ))}
      </ul>
    </section>
  );
}
