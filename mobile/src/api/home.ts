/**
 * Home feed API stub — aligned to patched OpenAPI spec.
 *
 * GET /v1/home
 */

import { apiFetch } from './client';

export type HomeFeedClusterItem = {
  id: string;
  category: string;
  state: string;
  temporalType: string;
  title: string;
  summary: string;
  locationLabel: string;
  rawConfirmationCount: number;
  lastSeenAt: string;
};

export type ActiveNowItem = {
  id: string;
  category: string;
  subcategorySlug: string;
  state: string;
  temporalType: string;
  title: string;
  summary: string;
  locationLabel: string;
  rawConfirmationCount: number;
  affectedCount: number;
  observingCount: number;
  lastSeenAt: string;
};

export type OfficialUpdateItem = {
  id: string;
  officialPostType: string;
  category: string | null;
  title: string;
  body: string;
  startsAt: string | null;
  endsAt: string | null;
};

export type HomeFeedResponse = {
  activeNow: ActiveNowItem[];
  officialUpdates: OfficialUpdateItem[];
  recurringAtThisTime: HomeFeedClusterItem[];
  otherActiveSignals: HomeFeedClusterItem[];
  followedLocalityIds: string[];
};

export function getHomeFeed(): Promise<HomeFeedResponse> {
  return apiFetch('/v1/home', { method: 'GET' });
}
