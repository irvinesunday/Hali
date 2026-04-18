import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { getInstitutionSignal } from "../api/institution";
import { ErrorState } from "../components/ErrorState";
import { LoadingSkeleton } from "../components/LoadingSkeleton";
import { institutionKeys } from "../query/keys";
import { formatDurationSeconds } from "./signalFormatting";

// Institution-scoped cluster detail. Pulls from
// `GET /v1/institution/signals/:clusterId`, which the backend
// restricts to clusters in the caller's jurisdiction — an out-of-
// scope id returns 404, which surfaces here as the same error card
// as a transport failure (we deliberately don't leak the distinction
// between "forbidden" and "missing").
//
// Post-update (#203) and restoration-response (#204) actions slot in
// as peer cards on this screen; they read the same cluster query and
// invalidate it on success.
export function ClusterDetailScreen() {
  const { clusterId } = useParams<{ clusterId: string }>();

  const cluster = useQuery({
    queryKey: institutionKeys.signalDetail(clusterId ?? ""),
    queryFn: () => getInstitutionSignal(clusterId ?? ""),
    enabled: Boolean(clusterId),
  });

  if (!clusterId) {
    return (
      <ErrorState
        title="Signal not found"
        description="The signal link is missing an identifier. Return to the signals list and try again."
      />
    );
  }

  if (cluster.isLoading) {
    return <LoadingSkeleton label="Loading signal detail" rowCount={3} />;
  }

  if (cluster.isError || !cluster.data) {
    return (
      <ErrorState
        title="We couldn't load this signal."
        description="It may be outside your institution's scope, or the service is temporarily unavailable. Retry or return to the signals list."
        onRetry={() => {
          void cluster.refetch();
        }}
      />
    );
  }

  const signal = cluster.data;
  const timeActiveSeconds = signal.activatedAt
    ? Math.max(0, Math.floor((Date.now() - new Date(signal.activatedAt).getTime()) / 1000))
    : 0;

  return (
    <article className="max-w-3xl space-y-6">
      <header className="space-y-1">
        <h2 className="text-xl font-semibold text-foreground">
          {signal.title ?? "Untitled signal"}
        </h2>
        {signal.locationLabel ? (
          <p className="text-sm text-muted-foreground">{signal.locationLabel}</p>
        ) : null}
      </header>

      {signal.summary ? (
        <p className="text-sm leading-relaxed text-foreground">{signal.summary}</p>
      ) : null}

      <dl className="grid gap-3 sm:grid-cols-2">
        <DetailField label="State" value={signal.state} />
        <DetailField label="Category" value={signal.category} />
        <DetailField
          label="Response status"
          value={signal.responseStatus ?? "No response recorded yet"}
        />
        <DetailField label="Affected" value={String(signal.affectedCount)} />
        <DetailField label="Observing" value={String(signal.observingCount)} />
        {signal.activatedAt ? (
          <DetailField label="Active for" value={formatDurationSeconds(timeActiveSeconds)} />
        ) : null}
      </dl>

      <Link
        to="/signals"
        className="inline-block text-sm font-medium text-foreground underline underline-offset-2"
      >
        Back to signals
      </Link>
    </article>
  );
}

function DetailField({ label, value }: { readonly label: string; readonly value: string }) {
  return (
    <div className="space-y-0.5">
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="text-sm font-medium text-foreground">{value}</dd>
    </div>
  );
}
