// @hali/config — non-secret app constants shared across surfaces.
//
// Secrets, API keys, and environment-dependent values do not live
// here — see docs/arch/SECURITY_POSTURE.md §5.

// The eight canonical civic categories in Hali. Mirrors
// src/Hali.Domain/Enums/CivicCategory.cs and the OpenAPI enum.
export const CIVIC_CATEGORIES = [
  "roads",
  "transport",
  "electricity",
  "water",
  "environment",
  "safety",
  "governance",
  "infrastructure",
] as const;

export type CivicCategory = (typeof CIVIC_CATEGORIES)[number];

// Signal lifecycle states emitted on the wire. Mirrors the backend
// SignalState enum's snake_case serialisation.
export const SIGNAL_STATES = [
  "unconfirmed",
  "active",
  "possible_restoration",
  "resolved",
] as const;

export type SignalState = (typeof SIGNAL_STATES)[number];

// Composer character limit for the citizen signal-submit free-text
// field. Mirrors the backend max-length constraint in the signal
// submit DTO (see SignalPreviewRequest.freeText in 02_openapi.yaml
// and apps/citizen-mobile/src/config/constants.ts). Shared here so
// any future web submission UI uses the same value.
export const SIGNAL_TEXT_MAX_LENGTH = 500;

// Maximum wards a citizen can follow. Mirrors the rule in
// CLAUDE.md and the WardFollow service constraint.
export const MAX_WARDS_FOLLOWED = 5;
