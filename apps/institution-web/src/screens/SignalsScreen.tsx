import { useInfiniteQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { getInstitutionSignals } from "../api/institution";
import type { InstitutionSignalListItem, InstitutionSignalsResponse } from "../api/types";
import { EmptyState } from "../components/EmptyState";
import { ErrorState } from "../components/ErrorState";
import { LoadingSkeleton } from "../components/LoadingSkeleton";
import { institutionKeys } from "../query/keys";
import { TelemetryEvents } from "../telemetry/events";
import { useQueryTelemetry } from "../telemetry/useQueryTelemetry";
import { formatDurationSeconds } from "./signalFormatting";

// Live signals list for an institution. Pulls from
// `GET /v1/institution/clusters` — the backend has already restricted
// results to clusters in scope, so this screen never has to filter
// them client-side. Each row links to the cluster detail route
// (`/signals/:clusterId`), which is the entry point the #202
// acceptance criteria call for.
//
// Pagination uses React Query's `useInfiniteQuery` driven by the
// server's cursor. The first page fetches with no cursor; subsequent
// pages pass `nextCursor` from the previous page. The "Load more"
// button is hidden once the server reports the list is exhausted
// (nextCursor === null).
export function SignalsScreen() {
  const signals = useInfiniteQuery<InstitutionSignalsResponse, Error>({
    queryKey: institutionKeys.signalsList({}),
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam }) => getInstitutionSignals({ cursor: pageParam as string | undefined }),
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  });

  useQueryTelemetry({
    startedEvent: TelemetryEvents.SignalsBoardLoadStarted,
    completedEvent: TelemetryEvents.SignalsBoardLoadCompleted,
    failedEvent: TelemetryEvents.SignalsBoardLoadFailed,
    isPending: signals.isPending,
    isSuccess: signals.isSuccess,
    isError: signals.isError,
    error: signals.error,
  });

  if (signals.isLoading) {
    return <LoadingSkeleton label="Loading signals" rowCount={5} />;
  }

  if (signals.isError || !signals.data) {
    return (
      <ErrorState
        title="We couldn't load your signals."
        description="The signals service is not responding. Check your connection and retry."
        onRetry={() => {
          void signals.refetch();
        }}
      />
    );
  }

  const items = signals.data.pages.flatMap((page) => page.items);
  if (items.length === 0) {
    return (
      <EmptyState
        title="No active signals in your scope"
        description="All quiet across your jurisdictions right now. New citizen reports and official updates will appear here as they come in."
      />
    );
  }

  return (
    <section aria-label="Live signals" className="space-y-4">
      <ul className="space-y-3">
        {items.map((item) => (
          <SignalRow key={item.id} item={item} />
        ))}
      </ul>
      {signals.hasNextPage ? (
        <button
          type="button"
          onClick={() => {
            void signals.fetchNextPage();
          }}
          disabled={signals.isFetchingNextPage}
          className="rounded-md border border-border bg-background px-4 py-2 text-sm font-medium text-foreground hover:bg-muted disabled:cursor-wait disabled:opacity-60"
        >
          {signals.isFetchingNextPage ? "Loading more…" : "Load more"}
        </button>
      ) : null}
    </section>
  );
}

function SignalRow({ item }: { readonly item: InstitutionSignalListItem }) {
  return (
    <li data-testid="signal-row" className="rounded-md border border-border bg-card">
      <Link
        to={`/signals/${item.id}`}
        className="flex flex-col gap-2 p-4 hover:bg-muted focus-visible:bg-muted"
      >
        <div className="flex items-baseline justify-between gap-4">
          <span className="text-sm font-semibold text-foreground">{item.title}</span>
          <span className="text-xs text-muted-foreground">
            {item.area ? item.area.name : "Unassigned area"}
          </span>
        </div>
        <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
          <StatusMetric label="Category" value={item.category} />
          <StatusMetric label="Condition" value={item.condition} />
          <StatusMetric label="Trend" value={item.trend} />
          {item.responseStatus ? (
            <StatusMetric label="Response" value={item.responseStatus} />
          ) : null}
          <StatusMetric label="Affected" value={String(item.affectedCount)} />
          <StatusMetric label="Last 24h" value={String(item.recentReports24h)} />
          <StatusMetric label="Active for" value={formatDurationSeconds(item.timeActiveSeconds)} />
        </div>
      </Link>
    </li>
  );
}

function StatusMetric({ label, value }: { readonly label: string; readonly value: string }) {
  return (
    <span>
      {label}: <span className="font-medium text-foreground">{value}</span>
    </span>
  );
}
