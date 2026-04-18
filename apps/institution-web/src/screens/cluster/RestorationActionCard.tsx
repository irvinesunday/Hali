import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { FormEvent } from "react";
import { useEffect, useId, useRef, useState } from "react";
import { ApiError } from "../../api/client";
import { createOfficialPost } from "../../api/officialPosts";
import type {
  CivicCategorySlug,
  ClusterState,
  OfficialPostCreateRequest,
  OfficialPostResponse,
} from "../../api/types";
import {
  InstitutionWebFlagKeys,
  useFeatureFlag,
} from "../../featureFlags/FeatureFlagsProvider";
import { institutionKeys } from "../../query/keys";
import { emitEvent } from "../../telemetry/emit";
import { TelemetryEvents } from "../../telemetry/events";

// Institution restoration action per Hali doctrine:
//
//   - Institutions can only *claim* restoration — the cluster moves to
//     `possible_restoration`, not `resolved`.
//   - Final resolution requires citizen confirmation (≥60% of
//     restoration votes and ≥2 affected voters, enforced server-side).
//   - A restoration claim is a `live_update` post with
//     `isRestorationClaim: true`; the server handles the state
//     transition and emits the outbox event.
//
// The card is state-aware:
//   - `active`                 → "Mark as restored" CTA opens a
//                                confirmation composer.
//   - `possible_restoration`   → awaiting-citizen-confirmation banner
//                                with live ratio + vote counts.
//   - `resolved`               → static "Service restored" banner.
//   - anything else            → renders nothing.

const TITLE_MAX = 220;
const BODY_MAX = 5000;

export interface RestorationActionCardProps {
  readonly clusterId: string;
  readonly clusterState: ClusterState | string;
  readonly clusterCategory: CivicCategorySlug | string;
  // Widened to include `undefined` so the partial-deploy case (server
  // omits the field entirely) is part of the prop contract, not an
  // implicit assumption. AwaitingBanner distinguishes null vs undefined
  // for the user-facing hint.
  readonly restorationRatio: number | null | undefined;
  readonly restorationYesVotes: number | null | undefined;
  readonly restorationTotalVotes: number | null | undefined;
  readonly resolvedAt: string | null;
}

export function RestorationActionCard({
  clusterId,
  clusterState,
  clusterCategory,
  restorationRatio,
  restorationYesVotes,
  restorationTotalVotes,
  resolvedAt,
}: RestorationActionCardProps) {
  const [composerOpen, setComposerOpen] = useState(false);
  const restorationEnabled = useFeatureFlag(InstitutionWebFlagKeys.restorationClaimEnabled);

  if (clusterState === "resolved") {
    return <ResolvedBanner resolvedAt={resolvedAt} />;
  }

  if (clusterState === "possible_restoration") {
    return (
      <AwaitingBanner
        ratio={restorationRatio}
        yesVotes={restorationYesVotes}
        totalVotes={restorationTotalVotes}
      />
    );
  }

  if (clusterState !== "active") {
    return null;
  }

  // Read-only screens remain under the master gate; only the
  // mutation CTA is hidden when the kill switch flips. The composer
  // modal is not mounted so a user deep-linking to a stale tab
  // can't re-use a cached modal render.
  if (!restorationEnabled) {
    return null;
  }

  return (
    <section
      aria-labelledby={`${clusterId}-restoration-heading`}
      className="rounded-md border border-border bg-card p-4"
    >
      <h3
        id={`${clusterId}-restoration-heading`}
        className="text-sm font-semibold text-foreground"
      >
        Claim restoration
      </h3>
      <p className="mt-1 text-xs text-muted-foreground">
        Move this signal into citizen confirmation. Citizens within the cluster will be prompted
        to confirm service is restored — the signal stays open until enough confirm.
      </p>
      <button
        type="button"
        onClick={() => {
          emitEvent(TelemetryEvents.RestorationClaimStarted, {
            cluster_category: clusterCategory,
          });
          setComposerOpen(true);
        }}
        className="mt-3 rounded-md bg-foreground px-4 py-2 text-sm font-medium text-background hover:opacity-90"
      >
        Mark as restored
      </button>
      <RestorationClaimModal
        clusterId={clusterId}
        clusterCategory={clusterCategory}
        open={composerOpen}
        onClose={() => setComposerOpen(false)}
      />
    </section>
  );
}

function ResolvedBanner({ resolvedAt }: { readonly resolvedAt: string | null }) {
  const resolvedLabel = resolvedAt ? new Date(resolvedAt).toLocaleString() : null;
  return (
    <section
      aria-label="Restoration status"
      className="rounded-md border border-border bg-muted/40 p-4 text-sm text-foreground"
    >
      <p className="font-semibold">Service restored</p>
      <p className="mt-1 text-xs text-muted-foreground">
        Citizen confirmations reached the threshold and this signal is resolved.
        {resolvedLabel ? ` Closed ${resolvedLabel}.` : null}
      </p>
    </section>
  );
}

function AwaitingBanner({
  ratio,
  yesVotes,
  totalVotes,
}: {
  readonly ratio: number | null | undefined;
  readonly yesVotes: number | null | undefined;
  readonly totalVotes: number | null | undefined;
}) {
  // Guard with `Number.isFinite` rather than `!== null`: during a
  // partial deployment the backend may omit these fields entirely, in
  // which case JSON.parse leaves them `undefined` at runtime even
  // though TS models them as `number | null`. A bare `!== null` check
  // lets `undefined` through and produces a `NaN%` render.
  const hasRatio = typeof ratio === "number" && Number.isFinite(ratio);
  const hasYesVotes = typeof yesVotes === "number" && Number.isFinite(yesVotes);
  const hasTotalVotes = typeof totalVotes === "number" && Number.isFinite(totalVotes);
  const percent = hasRatio ? Math.round(ratio * 100) : null;
  const countsKnown = hasYesVotes && hasTotalVotes;

  // Distinguish "intentional absence" (ratio === null — server returned
  // the field, no votes yet) from "field missing at runtime" (undefined
  // or non-finite — partial deploy where the new payload shape isn't
  // out everywhere yet). The former is a real product state and gets a
  // user-facing copy; the latter is a transient deploy artifact and
  // gets the more honest "Data unavailable" hint so we don't lie to the
  // operator.
  const ratioHint = hasRatio
    ? null
    : ratio === null
      ? "No citizen responses yet."
      : "Data unavailable.";
  return (
    <section
      aria-label="Restoration status"
      className="rounded-md border border-amber-300/60 bg-amber-50/60 p-4 text-sm text-foreground dark:border-amber-300/30 dark:bg-amber-900/20"
    >
      <p className="font-semibold">Awaiting citizen confirmation</p>
      <p className="mt-1 text-xs text-muted-foreground">
        You&apos;ve claimed restoration on this signal. It resolves once enough affected citizens
        confirm (≥60% of restoration votes and at least 2 affected voters).
      </p>
      <dl className="mt-3 grid gap-2 sm:grid-cols-3">
        <Stat
          label="Confirmation ratio"
          value={percent !== null ? `${percent}%` : "—"}
          hint={ratioHint}
        />
        <Stat
          label="Yes votes"
          value={countsKnown ? String(yesVotes) : "—"}
        />
        <Stat
          label="Total responses"
          value={countsKnown ? String(totalVotes) : "—"}
        />
      </dl>
    </section>
  );
}

function Stat({
  label,
  value,
  hint,
}: {
  readonly label: string;
  readonly value: string;
  readonly hint?: string | null;
}) {
  return (
    <div className="space-y-0.5">
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="text-sm font-medium text-foreground">{value}</dd>
      {hint ? <p className="text-xs text-muted-foreground">{hint}</p> : null}
    </div>
  );
}

interface RestorationClaimModalProps {
  readonly clusterId: string;
  readonly clusterCategory: CivicCategorySlug | string;
  readonly open: boolean;
  readonly onClose: () => void;
}

function RestorationClaimModal({
  clusterId,
  clusterCategory,
  open,
  onClose,
}: RestorationClaimModalProps) {
  const titleInputId = useId();
  const bodyInputId = useId();
  const queryClient = useQueryClient();
  const firstFieldRef = useRef<HTMLInputElement>(null);

  const [title, setTitle] = useState("Service restored");
  const [body, setBody] = useState("");
  const [localError, setLocalError] = useState<string | null>(null);

  const mutation = useMutation<OfficialPostResponse, Error, OfficialPostCreateRequest>({
    mutationFn: (request) => createOfficialPost(request),
    onSuccess: (_response, request) => {
      emitEvent(TelemetryEvents.RestorationClaimCompleted, {
        cluster_category: request.category,
      });
      void queryClient.invalidateQueries({ queryKey: institutionKeys.signalDetail(clusterId) });
      onClose();
    },
    onError: (error, request) => {
      emitEvent(TelemetryEvents.RestorationClaimFailed, {
        cluster_category: request.category,
        status: error instanceof ApiError ? error.status : null,
      });
    },
  });

  // `claim.cancelled` means "closed without submitting" in the funnel.
  // Gate on `isIdle` so a close after a failed submit stays categorised
  // as `claim.failed` rather than double-counting as a cancel. Tag with
  // the same bounded `cluster_category` the rest of the restoration
  // events carry so breakdowns stay consistent.
  const handleClose = () => {
    if (mutation.isIdle) {
      emitEvent(TelemetryEvents.RestorationClaimCancelled, {
        cluster_category: clusterCategory,
      });
    }
    onClose();
  };

  // Reset the form + mutation status when the modal closes so a
  // reopened composer is blank and any stale server error is gone.
  // `mutation.reset` is stable per the TanStack Query v5 useMutation
  // contract, but not known-stable to React's dep checker — hence the
  // ref wrapper instead of listing `mutation` (which is a new object
  // every render and causes an infinite update loop).
  const resetMutationRef = useRef(mutation.reset);
  resetMutationRef.current = mutation.reset;
  useEffect(() => {
    if (open) {
      firstFieldRef.current?.focus();
    } else {
      setTitle("Service restored");
      setBody("");
      setLocalError(null);
      resetMutationRef.current();
    }
  }, [open]);

  // `handleClose` is a fresh closure each render; route through a ref
  // so the listener always sees the latest closure. The effect still
  // re-runs when `open` or `mutation.isPending` flip — that's
  // acceptable (add/remove listener is cheap) and keeps the Escape
  // gate bound to the pending state without a second ref.
  const handleCloseRef = useRef(handleClose);
  handleCloseRef.current = handleClose;
  useEffect(() => {
    if (!open) return;
    const handleKey = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !mutation.isPending) {
        handleCloseRef.current();
      }
    };
    window.addEventListener("keydown", handleKey);
    return () => window.removeEventListener("keydown", handleKey);
  }, [open, mutation.isPending]);

  if (!open) return null;

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setLocalError(null);

    const trimmedTitle = title.trim();
    const trimmedBody = body.trim();
    if (!trimmedTitle || !trimmedBody) {
      setLocalError("Title and body are required.");
      return;
    }

    emitEvent(TelemetryEvents.RestorationClaimSubmitted, {
      cluster_category: clusterCategory,
    });
    mutation.mutate({
      type: "live_update",
      category: clusterCategory,
      title: trimmedTitle,
      body: trimmedBody,
      relatedClusterId: clusterId,
      isRestorationClaim: true,
      responseStatus: "restoration_in_progress",
    });
  };

  const serverError = mutation.error instanceof ApiError
    ? `The backend rejected this restoration claim (${mutation.error.status}). Check the fields and retry.`
    : mutation.error
      ? "Something went wrong posting this restoration claim. Please retry."
      : null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby={`${titleInputId}-heading`}
      className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/40 px-4 py-8"
    >
      <div className="relative w-full max-w-xl rounded-lg border border-border bg-card p-6 shadow-lg">
        <div className="mb-4 flex items-start justify-between gap-4">
          <div>
            <h2 id={`${titleInputId}-heading`} className="text-lg font-semibold text-foreground">
              Claim restoration
            </h2>
            <p className="text-xs text-muted-foreground">
              Citizens will see this update and be asked to confirm whether service is actually
              restored. The signal only closes once enough of them confirm.
            </p>
          </div>
          <button
            type="button"
            onClick={handleClose}
            disabled={mutation.isPending}
            className="rounded-md px-2 py-1 text-sm text-muted-foreground hover:bg-muted disabled:opacity-60"
            aria-label="Close"
          >
            ✕
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1">
            <label htmlFor={titleInputId} className="block text-xs font-medium text-foreground">
              Title
            </label>
            <input
              id={titleInputId}
              ref={firstFieldRef}
              type="text"
              required
              maxLength={TITLE_MAX}
              value={title}
              onChange={(event) => setTitle(event.target.value)}
              className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-foreground/20"
            />
            <p className="text-xs text-muted-foreground">
              {title.length}/{TITLE_MAX}
            </p>
          </div>

          <div className="space-y-1">
            <label htmlFor={bodyInputId} className="block text-xs font-medium text-foreground">
              Body
            </label>
            <textarea
              id={bodyInputId}
              required
              maxLength={BODY_MAX}
              value={body}
              onChange={(event) => setBody(event.target.value)}
              rows={5}
              placeholder="Summarise what was restored and when. Citizens will see this verbatim."
              className="w-full resize-y rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-foreground/20"
            />
            <p className="text-xs text-muted-foreground">
              {body.length}/{BODY_MAX}
            </p>
          </div>

          {localError ? (
            <p role="alert" className="text-sm text-red-600">
              {localError}
            </p>
          ) : null}
          {serverError ? (
            <p role="alert" className="text-sm text-red-600">
              {serverError}
            </p>
          ) : null}

          <div className="flex items-center justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={handleClose}
              disabled={mutation.isPending}
              className="rounded-md border border-border bg-background px-4 py-2 text-sm font-medium text-foreground hover:bg-muted disabled:cursor-wait disabled:opacity-60"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={mutation.isPending}
              className="rounded-md bg-foreground px-4 py-2 text-sm font-medium text-background hover:opacity-90 disabled:cursor-wait disabled:opacity-60"
            >
              {mutation.isPending ? "Submitting…" : "Confirm restoration claim"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
