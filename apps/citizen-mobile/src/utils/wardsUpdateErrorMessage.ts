// apps/citizen-mobile/src/utils/wardsUpdateErrorMessage.ts
//
// Pure mapping from a followed-localities update `ApiError` to the
// toast text the wards settings screen renders. Extracted so the
// branch logic can be unit tested without React Native (mirrors the
// `composerGates` pattern).
//
// Backend contract (post-#158): the over-capacity case is signalled
// by `validation.max_followed_localities_exceeded`. All other errors
// fall through to the server-supplied message.

import {
  ERROR_CODES,
  isKnownErrorCode,
  type ApiError,
} from '../types/api';

export interface MapWardsUpdateErrorOptions {
  maxFollowedWards: number;
}

export function mapWardsUpdateErrorToToast(
  error: ApiError,
  { maxFollowedWards }: MapWardsUpdateErrorOptions,
): string {
  if (
    isKnownErrorCode(error.code) &&
    error.code === ERROR_CODES.VALIDATION_MAX_FOLLOWED_LOCALITIES_EXCEEDED
  ) {
    return `You can follow up to ${maxFollowedWards} areas.`;
  }
  return error.message;
}
