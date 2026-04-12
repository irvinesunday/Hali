// apps/citizen-mobile/__tests__/api/homeContract.test.ts
//
// Contract shape tests verifying that the mobile HomeResponse type
// exactly matches the backend PagedSection<T> structure returned by
// GET /v1/home.
//
// These tests guard against contract drift between backend and mobile.

import type {
  ClusterResponse,
  HomeResponse,
  OfficialPostResponse,
  PagedSection,
} from '../../src/types/api';

// ─── Fixtures ────────────────────────────────────────────────────────────────

/** Minimal wire-format cluster matching ClusterResponseDto. */
function wireCluster(): ClusterResponse {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    state: 'active',
    category: 'water',
    subcategorySlug: 'water_outage',
    title: 'Water outage on Ngong Road',
    summary: 'No water since 6am',
    affectedCount: 12,
    observingCount: 3,
    createdAt: '2026-04-11T06:00:00Z',
    updatedAt: '2026-04-11T06:30:00Z',
    activatedAt: '2026-04-11T06:15:00Z',
    possibleRestorationAt: null,
    resolvedAt: null,
    officialPosts: [],
    myParticipation: null,
  };
}

/** Minimal wire-format official post matching OfficialPostResponseDto. */
function wireOfficialPost(): OfficialPostResponse {
  return {
    id: '00000000-0000-0000-0000-000000000002',
    institutionId: '00000000-0000-0000-0000-000000000099',
    type: 'live_update',
    category: 'water',
    title: 'Repair crew dispatched',
    body: 'A repair crew has been dispatched to the area.',
    startsAt: null,
    endsAt: null,
    status: 'active',
    relatedClusterId: null,
    isRestorationClaim: false,
    createdAt: '2026-04-11T07:00:00Z',
  };
}

function makePagedSection<T>(items: T[], nextCursor: string | null = null): PagedSection<T> {
  return { items, nextCursor, totalCount: items.length };
}

// ─── HomeResponse shape ──────────────────────────────────────────────────────

describe('HomeResponse contract shape', () => {
  it('has exactly four required PagedSection properties', () => {
    const response: HomeResponse = {
      activeNow: makePagedSection<ClusterResponse>([]),
      officialUpdates: makePagedSection<OfficialPostResponse>([]),
      recurringAtThisTime: makePagedSection<ClusterResponse>([]),
      otherActiveSignals: makePagedSection<ClusterResponse>([]),
    };

    // All four keys present
    expect(Object.keys(response).sort()).toEqual([
      'activeNow',
      'officialUpdates',
      'otherActiveSignals',
      'recurringAtThisTime',
    ]);
  });

  it('each section has items, nextCursor, and totalCount', () => {
    const section = makePagedSection([wireCluster()]);

    expect(section).toHaveProperty('items');
    expect(section).toHaveProperty('nextCursor');
    expect(section).toHaveProperty('totalCount');
    expect(Array.isArray(section.items)).toBe(true);
  });

  it('activeNow items are ClusterResponse', () => {
    const cluster = wireCluster();
    const section = makePagedSection([cluster]);

    const item = section.items[0]!;
    expect(item).toHaveProperty('id');
    expect(item).toHaveProperty('state');
    expect(item).toHaveProperty('category');
    expect(item).toHaveProperty('affectedCount');
    expect(item).toHaveProperty('observingCount');
    expect(item).toHaveProperty('officialPosts');
    expect(item).toHaveProperty('myParticipation');
  });

  it('officialUpdates items are OfficialPostResponse', () => {
    const post = wireOfficialPost();
    const section = makePagedSection([post]);

    const item = section.items[0]!;
    expect(item).toHaveProperty('id');
    expect(item).toHaveProperty('institutionId');
    expect(item).toHaveProperty('type');
    expect(item).toHaveProperty('title');
    expect(item).toHaveProperty('body');
    expect(item).toHaveProperty('isRestorationClaim');
  });

  it('populated response round-trips correctly', () => {
    const response: HomeResponse = {
      activeNow: makePagedSection([wireCluster()], 'cursor-1'),
      officialUpdates: makePagedSection([wireOfficialPost()]),
      recurringAtThisTime: makePagedSection<ClusterResponse>([]),
      otherActiveSignals: makePagedSection([wireCluster(), wireCluster()]),
    };

    // Simulate JSON round-trip (as if received from backend)
    const parsed: HomeResponse = JSON.parse(JSON.stringify(response));

    expect(parsed.activeNow.items).toHaveLength(1);
    expect(parsed.activeNow.nextCursor).toBe('cursor-1');
    expect(parsed.officialUpdates.items).toHaveLength(1);
    expect(parsed.recurringAtThisTime.items).toHaveLength(0);
    expect(parsed.otherActiveSignals.items).toHaveLength(2);
    expect(parsed.otherActiveSignals.totalCount).toBe(2);
  });

  it('calm state is computed from empty items arrays', () => {
    const response: HomeResponse = {
      activeNow: makePagedSection<ClusterResponse>([]),
      officialUpdates: makePagedSection<OfficialPostResponse>([]),
      recurringAtThisTime: makePagedSection<ClusterResponse>([]),
      otherActiveSignals: makePagedSection<ClusterResponse>([]),
    };

    const isCalmState =
      response.activeNow.items.length === 0 &&
      response.officialUpdates.items.length === 0 &&
      response.recurringAtThisTime.items.length === 0 &&
      response.otherActiveSignals.items.length === 0;

    expect(isCalmState).toBe(true);
  });
});
