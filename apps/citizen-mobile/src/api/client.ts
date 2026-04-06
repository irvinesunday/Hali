// apps/citizen-mobile/src/api/client.ts
//
// Base API client — raw fetch with:
//   - Bearer token attachment from SecureStore
//   - 401 silent refresh with request queuing (thundering-herd prevention)
//   - Auth failure handler registration
//   - Result<T, ApiError> return shape — never throws to callers
//
// Canonical types (ApiError, Result) live in src/types/api.ts — do not redeclare.

import * as SecureStore from 'expo-secure-store';
import { API_BASE_URL } from '../config/env';
import { SECURE_STORE_KEYS } from '../config/constants';
import type { ApiError, Result } from '../types/api';

// ─── Internal state ──────────────────────────────────────────────────────────

interface QueuedRequest {
  resolve: (token: string) => void;
  reject: (error: ApiError) => void;
}

let isRefreshing = false;
const refreshQueue: QueuedRequest[] = [];
let authFailureHandler: (() => void) | null = null;

// ─── Auth failure handler registration ───────────────────────────────────────

export function registerAuthFailureHandler(handler: () => void): void {
  authFailureHandler = handler;
}

// Exported for tests only — resets module state between tests.
export function __resetClientForTests(): void {
  isRefreshing = false;
  refreshQueue.length = 0;
  authFailureHandler = null;
}

// ─── SecureStore helpers ─────────────────────────────────────────────────────

export async function getAccessToken(): Promise<string | null> {
  return SecureStore.getItemAsync(SECURE_STORE_KEYS.ACCESS_TOKEN);
}

export async function getRefreshToken(): Promise<string | null> {
  return SecureStore.getItemAsync(SECURE_STORE_KEYS.REFRESH_TOKEN);
}

export async function persistTokens(
  accessToken: string,
  refreshToken: string,
): Promise<void> {
  await Promise.all([
    SecureStore.setItemAsync(SECURE_STORE_KEYS.ACCESS_TOKEN, accessToken),
    SecureStore.setItemAsync(SECURE_STORE_KEYS.REFRESH_TOKEN, refreshToken),
  ]);
}

export async function clearTokens(): Promise<void> {
  await Promise.all([
    SecureStore.deleteItemAsync(SECURE_STORE_KEYS.ACCESS_TOKEN),
    SecureStore.deleteItemAsync(SECURE_STORE_KEYS.REFRESH_TOKEN),
    SecureStore.deleteItemAsync(SECURE_STORE_KEYS.ACCOUNT_ID),
  ]);
}

// ─── Error construction ──────────────────────────────────────────────────────

/**
 * Normalise the three error body shapes used across the Hali backend:
 *   1. { error: "..." }                        — most endpoints
 *   2. { error: "...", code: "..." }           — clusters / official posts 422s
 *   3. { code: "...", message: "..." }         — signals rate limiter
 *
 * Returns a unified ApiError with both `code` and `message` populated.
 * Prefers `message` over `error` when both are present; prefers `code` over
 * a default when provided.
 */
function buildApiError(status: number, body: unknown): ApiError {
  if (body !== null && typeof body === 'object') {
    const b = body as Record<string, unknown>;
    const code = typeof b.code === 'string' ? b.code : 'unknown_error';
    const message =
      typeof b.message === 'string'
        ? b.message
        : typeof b.error === 'string'
          ? b.error
          : 'An unexpected error occurred.';
    return { status, code, message };
  }
  return {
    status,
    code: 'unknown_error',
    message: 'An unexpected error occurred.',
  };
}

const NETWORK_ERROR: ApiError = {
  status: 0,
  code: 'network_error',
  message: 'Unable to reach the server. Check your connection and try again.',
};

// ─── Core fetch wrapper — all HTTP traffic routes through here ───────────────

async function executeRequest<T>(
  path: string,
  method: string,
  body: Record<string, unknown> | null,
  headers: Record<string, string>,
): Promise<Result<T, ApiError>> {
  try {
    const response = await fetch(`${API_BASE_URL}${path}`, {
      method,
      headers,
      body: body !== null ? JSON.stringify(body) : undefined,
    });

    if (!response.ok) {
      const rawBody: unknown = await response.json().catch(() => null);
      return { ok: false, error: buildApiError(response.status, rawBody) };
    }

    if (response.status === 204) {
      return { ok: true, value: undefined as unknown as T };
    }

    const data = (await response.json()) as T;
    return { ok: true, value: data };
  } catch {
    return { ok: false, error: NETWORK_ERROR };
  }
}

// ─── Refresh queue drain ─────────────────────────────────────────────────────

function flushQueue(newToken: string | null, error: ApiError | null): void {
  for (const queued of refreshQueue) {
    if (newToken !== null) {
      queued.resolve(newToken);
    } else {
      // error is non-null whenever newToken is null — asserted by callers
      queued.reject(error as ApiError);
    }
  }
  refreshQueue.length = 0;
}

// ─── Token refresh — single-flight, routes through executeRequest ────────────

async function attemptTokenRefresh(): Promise<string> {
  // Join queue if a refresh is already in flight
  if (isRefreshing) {
    return new Promise<string>((resolve, reject) => {
      refreshQueue.push({ resolve, reject });
    });
  }

  isRefreshing = true;

  const refreshToken = await getRefreshToken();
  if (!refreshToken) {
    const err: ApiError = {
      status: 401,
      code: 'no_refresh_token',
      message: 'Session expired. Please sign in again.',
    };
    // Flush before clearing the flag so concurrent callers see the failure
    // rather than starting a second refresh cycle.
    flushQueue(null, err);
    isRefreshing = false;
    throw err;
  }

  // Refresh request routes through executeRequest like every other call
  const result = await executeRequest<{
    accessToken: string;
    refreshToken: string;
  }>('/v1/auth/refresh', 'POST', { refreshToken }, {
    'Content-Type': 'application/json',
  });

  if (!result.ok) {
    flushQueue(null, result.error);
    isRefreshing = false;
    throw result.error;
  }

  await persistTokens(result.value.accessToken, result.value.refreshToken);
  flushQueue(result.value.accessToken, null);
  isRefreshing = false;
  return result.value.accessToken;
}

// ─── Public request function ─────────────────────────────────────────────────

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: Record<string, unknown> | null;
  headers?: Record<string, string>;
  /** Pass false to skip attaching the Authorization header (auth endpoints). */
  withAuth?: boolean;
}

async function buildHeaders(
  withAuth: boolean,
  extra: Record<string, string>,
): Promise<Record<string, string>> {
  const h: Record<string, string> = {
    'Content-Type': 'application/json',
    ...extra,
  };
  if (withAuth) {
    const token = await getAccessToken();
    if (token) {
      h.Authorization = `Bearer ${token}`;
    }
  }
  return h;
}

export async function apiRequest<T>(
  path: string,
  options: RequestOptions = {},
): Promise<Result<T, ApiError>> {
  const {
    method = 'GET',
    body = null,
    headers: extraHeaders = {},
    withAuth = true,
  } = options;

  const firstAttempt = await executeRequest<T>(
    path,
    method,
    body,
    await buildHeaders(withAuth, extraHeaders),
  );

  if (firstAttempt.ok) return firstAttempt;

  // Only retry on 401 for requests that carried auth
  if (firstAttempt.error.status !== 401 || !withAuth) {
    return firstAttempt;
  }

  // Attempt silent refresh + retry once
  let newToken: string;
  try {
    newToken = await attemptTokenRefresh();
  } catch {
    // Refresh failed — clear session and notify app
    await clearTokens();
    if (authFailureHandler !== null) {
      authFailureHandler();
    }
    return {
      ok: false,
      error: {
        status: 401,
        code: 'session_expired',
        message: 'Your session has expired. Please sign in again.',
      },
    };
  }

  const retryHeaders = await buildHeaders(true, extraHeaders);
  retryHeaders.Authorization = `Bearer ${newToken}`;
  return executeRequest<T>(path, method, body, retryHeaders);
}
