// apps/citizen-mobile/src/utils/contextWindow.ts
//
// Pure helpers for the 2-minute "Add Further Context" window.
//
// Background: the backend enforces a context-edit window via
// participation.CreatedAt + ContextEditWindowMinutes (default 2). The
// cluster response does NOT communicate this window to the client, and
// it does not expose the user's prior participation either. So the
// mobile must track when the user tapped "I'm Affected" locally and
// hide the "Add Further Context" affordance after the window closes.
//
// These functions are pure (no Date.now() side effects — caller passes
// `now`) so they can be unit tested without time mocking.

/** 2 minutes in milliseconds — matches the backend default. */
export const CONTEXT_WINDOW_MS = 2 * 60 * 1000;

/**
 * Returns true if the context window is still open relative to `now`.
 * Returns false when no participation timestamp has been recorded yet.
 */
export function isContextWindowOpen(
  affectedAt: number | null,
  now: number,
): boolean {
  if (affectedAt === null) return false;
  return now - affectedAt < CONTEXT_WINDOW_MS;
}

/**
 * Returns the number of seconds remaining in the window, clamped at 0.
 * Returns 0 when there is no recorded timestamp.
 */
export function secondsRemaining(
  affectedAt: number | null,
  now: number,
): number {
  if (affectedAt === null) return 0;
  const elapsed = now - affectedAt;
  const remaining = CONTEXT_WINDOW_MS - elapsed;
  return Math.max(0, Math.ceil(remaining / 1000));
}
