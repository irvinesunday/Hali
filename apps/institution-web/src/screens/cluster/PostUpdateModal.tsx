import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { FormEvent } from "react";
import { useEffect, useId, useRef, useState } from "react";
import { ApiError } from "../../api/client";
import { createOfficialPost } from "../../api/officialPosts";
import type {
  CivicCategorySlug,
  OfficialPostCreateRequest,
  OfficialPostResponse,
  OfficialPostResponseStatus,
  OfficialPostSeverity,
  OfficialPostType,
} from "../../api/types";
import { institutionKeys } from "../../query/keys";
import { emitEvent } from "../../telemetry/emit";
import { TelemetryEvents } from "../../telemetry/events";

// Modal-driven composer for the three official-update kinds defined by
// `POST /v1/official-posts`:
//
//  - `live_update` — optional `responseStatus` from the canonical 6-value
//    set; other type-specific fields rejected by the backend.
//  - `scheduled_disruption` — optional `severity` (minor/moderate/major)
//    plus `startsAt` / `endsAt` datetimes.
//  - `advisory_public_notice` — title + body only.
//
// The server derives `institutionId` from the JWT. The caller supplies
// `relatedClusterId` and the cluster's own `category` so the post
// anchors to the signal we're viewing. Post-save we invalidate the
// cluster detail query so the freshly-authored post surfaces in the
// list without an explicit refetch.
//
// Field caps mirror `OfficialPostCreateRequest` (title 220, body 5000)
// so validation bites before the network round-trip.

const TITLE_MAX = 220;
const BODY_MAX = 5000;

const RESPONSE_STATUS_OPTIONS: ReadonlyArray<{ value: OfficialPostResponseStatus; label: string }> = [
  { value: "acknowledged", label: "Acknowledged" },
  { value: "teams_dispatched", label: "Teams dispatched" },
  { value: "teams_on_site", label: "Teams on site" },
  { value: "work_ongoing", label: "Work ongoing" },
  { value: "restoration_in_progress", label: "Restoration in progress" },
  { value: "service_restored", label: "Service restored" },
];

const SEVERITY_OPTIONS: ReadonlyArray<{ value: OfficialPostSeverity; label: string }> = [
  { value: "minor", label: "Minor" },
  { value: "moderate", label: "Moderate" },
  { value: "major", label: "Major" },
];

const TYPE_OPTIONS: ReadonlyArray<{ value: OfficialPostType; label: string; hint: string }> = [
  {
    value: "live_update",
    label: "Live update",
    hint: "Status change on an active incident — teams dispatched, restored, etc.",
  },
  {
    value: "scheduled_disruption",
    label: "Scheduled disruption",
    hint: "Planned outage or maintenance window with a known start/end.",
  },
  {
    value: "advisory_public_notice",
    label: "Advisory / public notice",
    hint: "General notice with no live status or planned window.",
  },
];

export interface PostUpdateModalProps {
  readonly clusterId: string;
  readonly clusterCategory: CivicCategorySlug | string;
  readonly open: boolean;
  readonly onClose: () => void;
}

export function PostUpdateModal({ clusterId, clusterCategory, open, onClose }: PostUpdateModalProps) {
  const titleInputId = useId();
  const bodyInputId = useId();
  const startsAtId = useId();
  const endsAtId = useId();
  const responseStatusId = useId();
  const severityId = useId();

  const queryClient = useQueryClient();
  const firstFieldRef = useRef<HTMLInputElement>(null);

  const [type, setType] = useState<OfficialPostType>("live_update");
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [startsAt, setStartsAt] = useState("");
  const [endsAt, setEndsAt] = useState("");
  const [responseStatus, setResponseStatus] = useState<OfficialPostResponseStatus | "">("");
  const [severity, setSeverity] = useState<OfficialPostSeverity | "">("");
  const [localError, setLocalError] = useState<string | null>(null);

  const resetForm = () => {
    setType("live_update");
    setTitle("");
    setBody("");
    setStartsAt("");
    setEndsAt("");
    setResponseStatus("");
    setSeverity("");
    setLocalError(null);
  };

  const mutation = useMutation<OfficialPostResponse, Error, OfficialPostCreateRequest>({
    mutationFn: (request) => createOfficialPost(request),
    onSuccess: (_response, request) => {
      emitEvent(TelemetryEvents.OfficialPostCreateCompleted, { post_type: request.type });
      void queryClient.invalidateQueries({ queryKey: institutionKeys.signalDetail(clusterId) });
      onClose();
    },
    onError: (error, request) => {
      emitEvent(TelemetryEvents.OfficialPostCreateFailed, {
        post_type: request.type,
        status: error instanceof ApiError ? error.status : null,
      });
    },
  });

  // Distinguishes a deliberate close (Cancel / ✕ / Escape) from the
  // post-success unmount the success handler triggers above. Without
  // this we would emit `draft.cancelled` on every successful submit.
  const handleClose = () => {
    if (!mutation.isSuccess && !mutation.isPending) {
      emitEvent(TelemetryEvents.OfficialPostDraftCancelled);
    }
    onClose();
  };

  useEffect(() => {
    if (open) {
      emitEvent(TelemetryEvents.OfficialPostDraftStarted, { cluster_category: clusterCategory });
      firstFieldRef.current?.focus();
    } else {
      resetForm();
    }
  }, [open, clusterCategory]);

  // handleClose is a fresh closure each render; route the keydown
  // handler through a ref so the listener attaches once per open and
  // doesn't churn on every pending-state flip.
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

    // The wire contract makes startsAt/endsAt nullable (the backend
    // accepts a scheduled_disruption with no window) but the UX intent
    // for this kind is a planned, time-bound disruption — without a
    // schedule the operator should be picking advisory_public_notice
    // instead. Enforce here so the form guides the choice.
    if (type === "scheduled_disruption" && (!startsAt || !endsAt)) {
      setLocalError("Scheduled disruptions need both a start and end time.");
      return;
    }

    const request: OfficialPostCreateRequest = {
      type,
      category: clusterCategory,
      title: trimmedTitle,
      body: trimmedBody,
      relatedClusterId: clusterId,
      ...(type === "live_update" && responseStatus ? { responseStatus } : {}),
      ...(type === "scheduled_disruption"
        ? {
            startsAt: new Date(startsAt).toISOString(),
            endsAt: new Date(endsAt).toISOString(),
            ...(severity ? { severity } : {}),
          }
        : {}),
    };

    emitEvent(TelemetryEvents.OfficialPostCreateSubmitted, { post_type: request.type });
    mutation.mutate(request);
  };

  const serverError = mutation.error instanceof ApiError
    ? `The backend rejected this update (${mutation.error.status}). Check the fields and retry.`
    : mutation.error
      ? "Something went wrong posting this update. Please retry."
      : null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby={`${titleInputId}-heading`}
      className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/40 px-4 py-8"
    >
      {/*
        Backdrop is decorative — Escape + the explicit ✕ button drive
        close, so we do not wire a click-to-close on the backdrop. Doing
        so previously required a focusable <button> wrapper that some
        assistive tech surfaced as an interactive control.
      */}
      <div className="relative w-full max-w-xl rounded-lg border border-border bg-card p-6 shadow-lg">
        <div className="mb-4 flex items-start justify-between gap-4">
          <div>
            <h2 id={`${titleInputId}-heading`} className="text-lg font-semibold text-foreground">
              Post an official update
            </h2>
            <p className="text-xs text-muted-foreground">
              Citizens within this cluster&apos;s scope will see this update next to the signal.
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
          <fieldset className="space-y-2">
            <legend className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
              Update kind
            </legend>
            <div className="space-y-2">
              {TYPE_OPTIONS.map((option) => {
                const radioId = `${titleInputId}-type-${option.value}`;
                return (
                  <label
                    key={option.value}
                    htmlFor={radioId}
                    aria-label={option.label}
                    className="flex cursor-pointer items-start gap-2 rounded-md border border-border bg-background p-2 text-sm hover:bg-muted has-[:checked]:border-foreground"
                  >
                    <input
                      id={radioId}
                      type="radio"
                      name="post-type"
                      value={option.value}
                      checked={type === option.value}
                      onChange={() => setType(option.value)}
                      className="mt-1"
                    />
                    <span className="flex flex-col">
                      <span className="font-medium text-foreground">{option.label}</span>
                      <span className="text-xs text-muted-foreground">{option.hint}</span>
                    </span>
                  </label>
                );
              })}
            </div>
          </fieldset>

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
              className="w-full resize-y rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-foreground/20"
            />
            <p className="text-xs text-muted-foreground">
              {body.length}/{BODY_MAX}
            </p>
          </div>

          {type === "live_update" ? (
            <div className="space-y-1">
              <label htmlFor={responseStatusId} className="block text-xs font-medium text-foreground">
                Response status (optional)
              </label>
              <select
                id={responseStatusId}
                value={responseStatus}
                onChange={(event) => setResponseStatus(event.target.value as OfficialPostResponseStatus | "")}
                className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-foreground/20"
              >
                <option value="">No status change</option>
                {RESPONSE_STATUS_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </div>
          ) : null}

          {type === "scheduled_disruption" ? (
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1">
                <label htmlFor={startsAtId} className="block text-xs font-medium text-foreground">
                  Starts
                </label>
                <input
                  id={startsAtId}
                  type="datetime-local"
                  required
                  value={startsAt}
                  onChange={(event) => setStartsAt(event.target.value)}
                  className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-foreground/20"
                />
              </div>
              <div className="space-y-1">
                <label htmlFor={endsAtId} className="block text-xs font-medium text-foreground">
                  Ends
                </label>
                <input
                  id={endsAtId}
                  type="datetime-local"
                  required
                  value={endsAt}
                  onChange={(event) => setEndsAt(event.target.value)}
                  className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-foreground/20"
                />
              </div>
              <div className="space-y-1 sm:col-span-2">
                <label htmlFor={severityId} className="block text-xs font-medium text-foreground">
                  Severity (optional)
                </label>
                <select
                  id={severityId}
                  value={severity}
                  onChange={(event) => setSeverity(event.target.value as OfficialPostSeverity | "")}
                  className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-foreground/20"
                >
                  <option value="">Not specified</option>
                  {SEVERITY_OPTIONS.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          ) : null}

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
              {mutation.isPending ? "Posting…" : "Post update"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
