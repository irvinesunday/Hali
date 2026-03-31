/**
 * Base API client for the Hali citizen app.
 *
 * Handles:
 * - Base URL and versioned prefix
 * - Authorization header injection
 * - Automatic token refresh on 401 (Flow B from mobile_screen_inventory.md)
 * - Idempotency-Key header for mutations
 */

const API_BASE_URL = process.env.EXPO_PUBLIC_API_BASE_URL ?? 'http://localhost:8080';

export type TokenStore = {
  getAccessToken: () => string | null;
  getRefreshToken: () => string | null;
  setTokens: (accessToken: string, refreshToken: string) => void;
  clearTokens: () => void;
};

let tokenStore: TokenStore | null = null;

export function configureTokenStore(store: TokenStore): void {
  tokenStore = store;
}

export type ApiError = {
  status: number;
  code?: string;
  message?: string;
};

async function refreshAccessToken(): Promise<boolean> {
  if (!tokenStore) return false;
  const refreshToken = tokenStore.getRefreshToken();
  if (!refreshToken) return false;

  const response = await fetch(`${API_BASE_URL}/v1/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  });

  if (!response.ok) {
    tokenStore.clearTokens();
    return false;
  }

  const data = await response.json();
  tokenStore.setTokens(data.accessToken, data.refreshToken);
  return true;
}

export async function apiFetch<T>(
  path: string,
  options: RequestInit & { idempotencyKey?: string } = {},
  retryOnUnauthorized = true,
): Promise<T> {
  const { idempotencyKey, ...fetchOptions } = options;

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(fetchOptions.headers as Record<string, string>),
  };

  if (tokenStore?.getAccessToken()) {
    headers['Authorization'] = `Bearer ${tokenStore.getAccessToken()}`;
  }

  if (idempotencyKey) {
    headers['Idempotency-Key'] = idempotencyKey;
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...fetchOptions,
    headers,
  });

  if (response.status === 401 && retryOnUnauthorized) {
    const refreshed = await refreshAccessToken();
    if (refreshed) {
      return apiFetch<T>(path, options, false);
    }
    throw { status: 401, code: 'unauthorized', message: 'Session expired' } as ApiError;
  }

  if (!response.ok) {
    let errorBody: { code?: string; message?: string } = {};
    try {
      errorBody = await response.json();
    } catch {
      // non-JSON error body
    }
    throw { status: response.status, ...errorBody } as ApiError;
  }

  if (response.status === 204) {
    return undefined as unknown as T;
  }

  return response.json() as Promise<T>;
}
