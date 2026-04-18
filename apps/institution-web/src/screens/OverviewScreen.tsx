import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { getInstitutionOverview } from "../api/institution";
import type { InstitutionArea, InstitutionOverviewSummary } from "../api/types";
import { EmptyState } from "../components/EmptyState";
import { ErrorState } from "../components/ErrorState";
import { LoadingSkeleton } from "../components/LoadingSkeleton";
import { institutionKeys } from "../query/keys";

// Institution home. Renders the summary counters and the top areas
// strip returned by `GET /v1/institution/overview`. Detail drill-in
// goes through the signals screen; from here the area rows are
// read-only snapshots.
export function OverviewScreen() {
  const overview = useQuery({
    queryKey: institutionKeys.overview(),
    queryFn: getInstitutionOverview,
  });

  if (overview.isLoading) {
    return <LoadingSkeleton label="Loading overview" rowCount={4} />;
  }

  if (overview.isError || !overview.data) {
    return (
      <ErrorState
        title="We couldn't load the overview."
        description="The institution summary service is not responding. Check your connection and retry."
        onRetry={() => {
          void overview.refetch();
        }}
      />
    );
  }

  const { summary, areas } = overview.data;

  return (
    <div className="space-y-8">
      <SummaryCards summary={summary} />
      <AreasStrip areas={areas} />
    </div>
  );
}

function SummaryCards({ summary }: { readonly summary: InstitutionOverviewSummary }) {
  const cards: ReadonlyArray<{ label: string; value: number; testId: string }> = [
    { label: "Active signals", value: summary.activeSignals, testId: "summary-active-signals" },
    { label: "Growing signals", value: summary.growingSignals, testId: "summary-growing-signals" },
    {
      label: "Updates posted today",
      value: summary.updatesPostedToday,
      testId: "summary-updates-today",
    },
    {
      label: "Stabilised today",
      value: summary.stabilisedToday,
      testId: "summary-stabilised-today",
    },
  ];

  return (
    <section aria-label="Summary" className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {cards.map((card) => (
        <article
          key={card.label}
          data-testid={card.testId}
          className="space-y-1 rounded-md border border-border bg-card p-4"
        >
          <p className="text-xs uppercase tracking-wide text-muted-foreground">{card.label}</p>
          <p className="text-2xl font-semibold text-foreground">{card.value}</p>
        </article>
      ))}
    </section>
  );
}

function AreasStrip({ areas }: { readonly areas: ReadonlyArray<InstitutionArea> }) {
  if (areas.length === 0) {
    return (
      <EmptyState
        title="No areas in scope yet"
        description="Once your institution's jurisdictions are attached, they appear here with their live condition."
      />
    );
  }

  return (
    <section aria-label="Top areas" className="space-y-3">
      <div className="flex items-baseline justify-between">
        <h2 className="text-base font-semibold text-foreground">Top areas</h2>
        <Link
          to="/areas"
          className="text-xs font-medium text-foreground underline underline-offset-2"
        >
          View all areas
        </Link>
      </div>
      <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {areas.map((area) => (
          <li
            key={area.id}
            className="rounded-md border border-border bg-card p-4"
            data-testid="overview-area-item"
          >
            <p className="text-sm font-semibold text-foreground">{area.name}</p>
            <p className="text-xs text-muted-foreground">
              Condition: <span className="font-medium text-foreground">{area.condition}</span>
            </p>
            <p className="text-xs text-muted-foreground">{area.activeSignals} active signals</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
