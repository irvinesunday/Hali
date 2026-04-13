// apps/citizen-mobile/__tests__/api/localities.test.ts
//
// Unit tests for the localities API service layer.

import { resolveByCoordinates } from '../../src/api/localities';

const mockApiRequest = jest.fn();

jest.mock('../../src/api/client', () => ({
  apiRequest: (...args: unknown[]) => mockApiRequest(...args),
}));

describe('resolveByCoordinates', () => {
  beforeEach(() => jest.clearAllMocks());

  it('calls the correct endpoint with latitude and longitude params', async () => {
    mockApiRequest.mockResolvedValueOnce({
      ok: true,
      value: {
        localityId: 'abc-123',
        wardName: 'South B',
        cityName: 'Nairobi',
      },
    });

    const result = await resolveByCoordinates(-1.3032, 36.8219);

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.value.localityId).toBe('abc-123');
      expect(result.value.wardName).toBe('South B');
    }
    expect(mockApiRequest).toHaveBeenCalledWith(
      expect.stringContaining('/v1/localities/resolve-by-coordinates'),
      expect.objectContaining({ method: 'GET' }),
    );
  });

  it('includes latitude and longitude as query params', async () => {
    mockApiRequest.mockResolvedValueOnce({ ok: true, value: {} });
    await resolveByCoordinates(-1.2921, 36.8219);
    const calledUrl = mockApiRequest.mock.calls[0][0] as string;
    expect(calledUrl).toContain('latitude=-1.2921');
    expect(calledUrl).toContain('longitude=36.8219');
  });

  it('returns err result on 404 (no locality at coordinates)', async () => {
    mockApiRequest.mockResolvedValueOnce({
      ok: false,
      error: { status: 404, code: 'not_found', message: 'No locality found' },
    });

    const result = await resolveByCoordinates(0, 0);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.error.status).toBe(404);
    }
  });

  it('returns err result on API failure', async () => {
    mockApiRequest.mockResolvedValueOnce({
      ok: false,
      error: { status: 500, code: 'server_error', message: 'Internal error' },
    });

    const result = await resolveByCoordinates(-1.3032, 36.8219);

    expect(result.ok).toBe(false);
  });
});
