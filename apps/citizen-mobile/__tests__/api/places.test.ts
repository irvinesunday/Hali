// apps/citizen-mobile/__tests__/api/places.test.ts
//
// Unit tests for the places API service layer (C11 fallback).

import { searchPlaces, reverseGeocodePoint } from '../../src/api/places';

const mockApiRequest = jest.fn();

jest.mock('../../src/api/client', () => ({
  apiRequest: (...args: unknown[]) => mockApiRequest(...args),
}));

describe('searchPlaces', () => {
  beforeEach(() => jest.clearAllMocks());

  it('calls GET /v1/places/search with a URL-encoded query', async () => {
    mockApiRequest.mockResolvedValueOnce({ ok: true, value: [] });

    await searchPlaces('Ngong Road');

    const calledUrl = mockApiRequest.mock.calls[0][0] as string;
    expect(calledUrl).toContain('/v1/places/search?q=');
    expect(calledUrl).toContain('Ngong%20Road');
    expect(mockApiRequest).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({ method: 'GET' }),
    );
  });

  it('trims whitespace before encoding the query', async () => {
    mockApiRequest.mockResolvedValueOnce({ ok: true, value: [] });

    await searchPlaces('  Uhuru Park  ');

    const calledUrl = mockApiRequest.mock.calls[0][0] as string;
    expect(calledUrl).toContain('Uhuru%20Park');
    expect(calledUrl).not.toContain('%20Uhuru'); // no leading-space encoding
  });

  it('returns the candidate list on success', async () => {
    mockApiRequest.mockResolvedValueOnce({
      ok: true,
      value: [
        {
          displayName: 'Ngong Road, Nairobi',
          latitude: -1.3,
          longitude: 36.78,
          localityId: 'abc-123',
          wardName: 'Nairobi West',
          cityName: 'Nairobi',
        },
      ],
    });

    const result = await searchPlaces('Ngong Road');

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.value).toHaveLength(1);
      expect(result.value[0].localityId).toBe('abc-123');
    }
  });

  it('propagates backend 400 (query_too_short) as an err result', async () => {
    mockApiRequest.mockResolvedValueOnce({
      ok: false,
      error: { status: 400, code: 'query_too_short', message: 'Query too short' },
    });

    const result = await searchPlaces('a');

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.code).toBe('query_too_short');
    }
  });
});

describe('reverseGeocodePoint', () => {
  beforeEach(() => jest.clearAllMocks());

  it('calls GET /v1/places/reverse with latitude and longitude params', async () => {
    mockApiRequest.mockResolvedValueOnce({
      ok: true,
      value: {
        displayName: 'Ngong Road, Nairobi West',
        latitude: -1.3,
        longitude: 36.78,
        localityId: 'abc-123',
        wardName: 'Nairobi West',
        cityName: 'Nairobi',
      },
    });

    await reverseGeocodePoint(-1.3, 36.78);

    const calledUrl = mockApiRequest.mock.calls[0][0] as string;
    expect(calledUrl).toContain('/v1/places/reverse?');
    expect(calledUrl).toContain('latitude=-1.3');
    expect(calledUrl).toContain('longitude=36.78');
  });

  it('surfaces 404 (locality_not_found) as an err result', async () => {
    mockApiRequest.mockResolvedValueOnce({
      ok: false,
      error: { status: 404, code: 'locality_not_found', message: 'No locality found' },
    });

    const result = await reverseGeocodePoint(0, 0);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.code).toBe('locality_not_found');
    }
  });
});
