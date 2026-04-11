// apps/citizen-mobile/__tests__/api/clusters.test.ts
//
// Unit tests for the cluster / home API service layer.
// Mocks the underlying apiRequest — no network calls.

import {
  getHome,
  participate,
  submitRestorationResponse,
} from '../../src/api/clusters';
import type {
  ApiError,
  ClusterResponse,
  HomeResponse,
  ParticipationRequest,
  PagedSection,
  Result,
  RestorationResponseRequest,
} from '../../src/types/api';

// ─── Mock apiRequest ─────────────────────────────────────────────────────────

const mockApiRequest = jest.fn();

jest.mock('../../src/api/client', () => ({
  apiRequest: (...args: unknown[]) => mockApiRequest(...args),
}));

// ─── Test fixtures ───────────────────────────────────────────────────────────

function okResult<T>(value: T): Result<T, ApiError> {
  return { ok: true, value };
}

function errResult(
  status: number,
  code: string,
  message: string,
): Result<never, ApiError> {
  return { ok: false, error: { status, code, message } };
}

function makeCluster(overrides: Partial<ClusterResponse> = {}): ClusterResponse {
  return {
    id: '11111111-1111-1111-1111-111111111111',
    state: 'active',
    category: 'water',
    subcategorySlug: 'water_outage',
    title: 'Water outage on Ngong Road',
    summary: 'No water since 6am',
    affectedCount: 12,
    observingCount: 3,
    createdAt: '2026-04-06T06:00:00Z',
    updatedAt: '2026-04-06T06:30:00Z',
    activatedAt: '2026-04-06T06:15:00Z',
    possibleRestorationAt: null,
    resolvedAt: null,
    officialPosts: [],
    myParticipation: null,
    ...overrides,
  };
}

function makeSection<T>(items: T[]): PagedSection<T> {
  return {
    items,
    nextCursor: null,
    totalCount: items.length,
  };
}

function emptyHome(): HomeResponse {
  return {
    activeNow: makeSection<ClusterResponse>([]),
    officialUpdates: makeSection([]),
    recurringAtThisTime: makeSection<ClusterResponse>([]),
    otherActiveSignals: makeSection<ClusterResponse>([]),
  };
}

// ─── getHome ─────────────────────────────────────────────────────────────────

describe('getHome', () => {
  beforeEach(() => {
    mockApiRequest.mockReset();
  });

  it('calls GET /v1/home with no query params by default', async () => {
    mockApiRequest.mockResolvedValueOnce(okResult(emptyHome()));

    await getHome();

    expect(mockApiRequest).toHaveBeenCalledTimes(1);
    expect(mockApiRequest).toHaveBeenCalledWith('/v1/home', { method: 'GET' });
  });

  it('appends a section query param when provided', async () => {
    mockApiRequest.mockResolvedValueOnce(okResult(emptyHome()));

    await getHome({ section: 'active_now' });

    expect(mockApiRequest).toHaveBeenCalledWith('/v1/home?section=active_now', {
      method: 'GET',
    });
  });

  it('appends both section and cursor when provided', async () => {
    mockApiRequest.mockResolvedValueOnce(okResult(emptyHome()));

    await getHome({
      section: 'other_active_signals',
      cursor: 'opaque-cursor-value',
    });

    expect(mockApiRequest).toHaveBeenCalledWith(
      '/v1/home?section=other_active_signals&cursor=opaque-cursor-value',
      { method: 'GET' },
    );
  });

  it('url-encodes the cursor value', async () => {
    mockApiRequest.mockResolvedValueOnce(okResult(emptyHome()));

    await getHome({ section: 'active_now', cursor: 'a+b/c=' });

    expect(mockApiRequest).toHaveBeenCalledWith(
      '/v1/home?section=active_now&cursor=a%2Bb%2Fc%3D',
      { method: 'GET' },
    );
  });

  it('returns the full four-section PagedSection response on success', async () => {
    const cluster1 = makeCluster({ id: 'c1', title: 'Outage #1' });
    const cluster2 = makeCluster({ id: 'c2', title: 'Outage #2' });
    const fullResponse: HomeResponse = {
      activeNow: makeSection([cluster1, cluster2]),
      officialUpdates: makeSection([]),
      recurringAtThisTime: makeSection([]),
      otherActiveSignals: makeSection([cluster1]),
    };
    mockApiRequest.mockResolvedValueOnce(okResult(fullResponse));

    const result = await getHome();

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.value.activeNow.items).toHaveLength(2);
    expect(result.value.activeNow.items[0]?.id).toBe('c1');
    expect(result.value.activeNow.totalCount).toBe(2);
    expect(result.value.officialUpdates.items).toHaveLength(0);
    expect(result.value.otherActiveSignals.items).toHaveLength(1);
  });

  it('returns the calm/empty response shape with every section items: []', async () => {
    mockApiRequest.mockResolvedValueOnce(okResult(emptyHome()));

    const result = await getHome();

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    // All four sections are PagedSection — never undefined or null.
    expect(result.value.activeNow.items).toEqual([]);
    expect(result.value.officialUpdates.items).toEqual([]);
    expect(result.value.recurringAtThisTime.items).toEqual([]);
    expect(result.value.otherActiveSignals.items).toEqual([]);
    // Empty sections have nextCursor: null and totalCount: 0.
    expect(result.value.activeNow.nextCursor).toBeNull();
    expect(result.value.activeNow.totalCount).toBe(0);
  });

  it('propagates apiRequest errors as Result.err without throwing', async () => {
    mockApiRequest.mockResolvedValueOnce(
      errResult(500, 'unknown_error', 'An unexpected error occurred.'),
    );

    const result = await getHome();

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error.status).toBe(500);
    expect(result.error.code).toBe('unknown_error');
  });
});

// ─── participate ─────────────────────────────────────────────────────────────

describe('participate', () => {
  beforeEach(() => {
    mockApiRequest.mockReset();
  });

  const clusterId = 'abc-123';
  const baseBody: ParticipationRequest = {
    type: 'affected',
    deviceHash: 'fake-device-hash',
    idempotencyKey: 'idem-001',
  };

  it('POSTs to /v1/clusters/{id}/participation with the URL-encoded id', async () => {
    mockApiRequest.mockResolvedValueOnce(okResult(undefined));

    await participate(clusterId, baseBody);

    expect(mockApiRequest).toHaveBeenCalledTimes(1);
    expect(mockApiRequest).toHaveBeenCalledWith(
      '/v1/clusters/abc-123/participation',
      expect.objectContaining({ method: 'POST' }),
    );
  });

  // Note: the wire body sends `type` as the PascalCase enum name the
  // backend's Enum.TryParse expects. The mobile snake_case ParticipationType
  // is converted via participationTypeToBackend before sending. This is
  // verified end-to-end here AND covered in unit tests at
  // __tests__/utils/participationApi.test.ts.
  it('passes the body with PascalCase wire type', async () => {
    mockApiRequest.mockResolvedValueOnce(okResult(undefined));

    await participate(clusterId, baseBody);

    const [, options] = mockApiRequest.mock.calls[0];
    expect(options.body).toEqual({
      type: 'Affected',
      deviceHash: 'fake-device-hash',
      idempotencyKey: 'idem-001',
    });
  });

  it('sends "observing" → "Observing" on the wire', async () => {
    mockApiRequest.mockResolvedValueOnce(okResult(undefined));

    await participate(clusterId, {
      type: 'observing',
      deviceHash: 'hash',
      idempotencyKey: 'k1',
    });

    const [, options] = mockApiRequest.mock.calls[0];
    expect(options.body.type).toBe('Observing');
  });

  it('sends "no_longer_affected" → "NoLongerAffected" on the wire', async () => {
    mockApiRequest.mockResolvedValueOnce(okResult(undefined));

    await participate(clusterId, {
      type: 'no_longer_affected',
      deviceHash: 'hash',
      idempotencyKey: 'k2',
    });

    const [, options] = mockApiRequest.mock.calls[0];
    // Critical contract assertion: underscores must be stripped, not just
    // case-folded, because Enum.TryParse does not understand snake_case.
    expect(options.body.type).toBe('NoLongerAffected');
  });

  it('url-encodes cluster ids that contain reserved characters', async () => {
    mockApiRequest.mockResolvedValueOnce(okResult(undefined));

    await participate('weird/id?&=', baseBody);

    expect(mockApiRequest).toHaveBeenCalledWith(
      '/v1/clusters/weird%2Fid%3F%26%3D/participation',
      expect.any(Object),
    );
  });

  it('returns the Result directly on success (ok: true, value: undefined)', async () => {
    mockApiRequest.mockResolvedValueOnce(okResult(undefined));

    const result = await participate(clusterId, baseBody);

    expect(result.ok).toBe(true);
  });

  it('returns Result.err on a 422 device_not_found error without throwing', async () => {
    mockApiRequest.mockResolvedValueOnce(
      errResult(422, 'device_not_found', 'Device not recognised.'),
    );

    const result = await participate(clusterId, baseBody);

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error.status).toBe(422);
    expect(result.error.code).toBe('device_not_found');
    expect(result.error.message).toBe('Device not recognised.');
  });

  it('returns Result.err on a network error without throwing', async () => {
    mockApiRequest.mockResolvedValueOnce(
      errResult(0, 'network_error', 'Unable to reach the server.'),
    );

    const result = await participate(clusterId, baseBody);

    expect(result.ok).toBe(false);
  });
});

// ─── submitRestorationResponse ───────────────────────────────────────────────

describe('submitRestorationResponse', () => {
  beforeEach(() => {
    mockApiRequest.mockReset();
  });

  const clusterId = 'cluster-xyz';

  it('POSTs to /v1/clusters/{id}/restoration-response', async () => {
    mockApiRequest.mockResolvedValueOnce(okResult(undefined));

    await submitRestorationResponse(clusterId, {
      response: 'restored',
      deviceHash: 'hash',
    });

    expect(mockApiRequest).toHaveBeenCalledWith(
      '/v1/clusters/cluster-xyz/restoration-response',
      expect.objectContaining({ method: 'POST' }),
    );
  });

  // The backend accepts exactly three response values verified against
  // ClustersController.cs and ParticipationService.cs. These tests lock
  // that contract so a renamed/removed value surfaces immediately.
  // Note: there is no 'restoration_no' wire value — the backend treats
  // the negative case as "still_affected" which re-records you as Affected.
  const validResponses: RestorationResponseRequest['response'][] = [
    'restored',
    'still_affected',
    'not_sure',
  ];

  it.each(validResponses)(
    'sends "%s" as the response field',
    async (value) => {
      mockApiRequest.mockResolvedValueOnce(okResult(undefined));

      await submitRestorationResponse(clusterId, {
        response: value,
        deviceHash: 'hash',
      });

      const [, options] = mockApiRequest.mock.calls[0];
      expect(options.body.response).toBe(value);
    },
  );

  it('includes deviceHash in the request body', async () => {
    mockApiRequest.mockResolvedValueOnce(okResult(undefined));

    await submitRestorationResponse(clusterId, {
      response: 'restored',
      deviceHash: 'specific-device-hash',
    });

    const [, options] = mockApiRequest.mock.calls[0];
    expect(options.body.deviceHash).toBe('specific-device-hash');
  });

  it('returns Result.err on 422 invalid_restoration_response without throwing', async () => {
    mockApiRequest.mockResolvedValueOnce(
      errResult(422, 'invalid_restoration_response', 'Invalid response value.'),
    );

    const result = await submitRestorationResponse(clusterId, {
      response: 'restored',
      deviceHash: 'hash',
    });

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error.code).toBe('invalid_restoration_response');
  });

  it('returns Result.err on 401 session_expired', async () => {
    mockApiRequest.mockResolvedValueOnce(
      errResult(401, 'session_expired', 'Your session has expired.'),
    );

    const result = await submitRestorationResponse(clusterId, {
      response: 'still_affected',
      deviceHash: 'hash',
    });

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error.status).toBe(401);
  });
});
