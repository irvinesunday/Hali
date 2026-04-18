import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getInstitutionSignal } from "../api/institution";
import type { OfficialPostResponse } from "../api/types";
import { ErrorState } from "../components/ErrorState";
import { LoadingSkeleton } from "../components/LoadingSkeleton";
import { institutionKeys } from "../query/keys";
import {
  InstitutionWebFlagKeys,
  useFeatureFlag,
} from "../featureFlags/FeatureFlagsProvider";
import { PostUpdateModal } from "./cluster/PostUpdateModal";
import { RestorationActionCard } from "./cluster/RestorationActionCard";
import { formatDurationSeconds } from "./signalFormatting";

// Institution-scoped cluster detail. Pulls from
// `GET /v1/institution/signals/:clusterId`, which the backend
// restricts to clusters in the caller's jurisdiction — an out-of-
// scope id returns 404, which surfaces here as the same error card
// as a transport failure (we deliberately don't leak the distinction
// between "forbidden" and "missing").
//
// Post-update (#203) is wired here as a peer card: the button opens
// `PostUpdateModal`, which invalidates this cluster's query on
// success so the new post surfaces in the list without an explicit
// refetch. Restoration (#204) sits below it as a state-aware card
// (`RestorationActionCard`) so the composer and citizen-confirmation
// banner share the same slot.
export function ClusterDetailScreen() {
  const { clusterId } = useParams<{ clusterId: string }>();
  const [postModalOpen, setPostModalOpen] = useState(false);
  const postUpdateEnabled = useFeatureFlag(InstitutionWebFlagKeys.postUpdateEnabled);

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
  const officialPosts = signal.officialPosts ?? [];

  return (
    <article className="max-w-3xl space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <h2 className="text-xl font-semibold text-foreground">
            {signal.title ?? "Untitled signal"}
          </h2>
          {signal.locationLabel ? (
            <p className="text-sm text-muted-foreground">{signal.locationLabel}</p>
          ) : null}
        </div>
        {postUpdateEnabled ? (
          <button
            type="button"
            onClick={() => setPostModalOpen(true)}
            className="shrink-0 rounded-md bg-foreground px-4 py-2 text-sm font-medium text-background hover:opacity-90"
          >
            Post an update
          </button>
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

      <RestorationActionCard
        clusterId={signal.id}
        clusterState={signal.state}
        clusterCategory={signal.category}
        restorationRatio={signal.restorationRatio}
        restorationYesVotes={signal.restorationYesVotes}
        restorationTotalVotes={signal.restorationTotalVotes}
        resolvedAt={signal.resolvedAt}
      />

      <section aria-label="Official updates" className="space-y-3">
        <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          Official updates
        </h3>
        {officialPosts.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No official updates posted yet. Use the button above to publish one.
          </p>
        ) : (
          <ul className="space-y-2">
            {officialPosts.map((post) => (
              <OfficialPostCard key={post.id} post={post} />
            ))}
          </ul>
        )}
      </section>

      <Link
        to="/signals"
        className="inline-block text-sm font-medium text-foreground underline underline-offset-2"
      >
        Back to signals
      </Link>

      {postUpdateEnabled ? (
        <PostUpdateModal
          clusterId={signal.id}
          clusterCategory={signal.category}
          open={postModalOpen}
          onClose={() => setPostModalOpen(false)}
        />
      ) : null}
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

function OfficialPostCard({ post }: { readonly post: OfficialPostResponse }) {
  return (
    <li
      data-testid="official-post-card"
      className="rounded-md border border-border bg-card p-3 text-sm"
    >
      <div className="flex items-baseline justify-between gap-2">
        <span className="font-medium text-foreground">{post.title}</span>
        <span className="text-xs uppercase tracking-wide text-muted-foreground">
          {post.type.replace(/_/g, " ")}
        </span>
      </div>
      <p className="mt-1 whitespace-pre-wrap text-muted-foreground">{post.body}</p>
      <div className="mt-2 flex flex-wrap gap-x-3 gap-y-1 text-xs text-muted-foreground">
        <span>Status: {post.status}</span>
        {post.responseStatus ? <span>Response: {post.responseStatus}</span> : null}
        {post.severity ? <span>Severity: {post.severity}</span> : null}
      </div>
    </li>
  );
}
