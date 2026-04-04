import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import * as SecureStore from 'expo-secure-store';
import { API_BASE_URL } from '../config/env';
import { SECURE_STORE_KEYS } from '../config/constants';
import type { TokenResponse } from '../types/api';

// Callback registered by AuthContext to handle forced logout on refresh failure
let _onAuthFailure: (() => void) | null = null;

export function registerAuthFailureHandler(handler: () => void): void {
  _onAuthFailure = handler;
}

const client = axios.create({
  baseURL: API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
  timeout: 15_000,
});

// ─── Request interceptor — attach access token ────────────────────────────────
client.interceptors.request.use(async (config: InternalAxiosRequestConfig) => {
  const token = await SecureStore.getItemAsync(SECURE_STORE_KEYS.ACCESS_TOKEN);
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// ─── Response interceptor — token refresh on 401 ─────────────────────────────
let isRefreshing = false;
let refreshQueue: Array<(token: string) => void> = [];

client.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & {
      _retried?: boolean;
    };

    if (error.response?.status !== 401 || originalRequest._retried) {
      return Promise.reject(error);
    }

    // Skip refresh loop for the refresh endpoint itself
    if (originalRequest.url?.includes('/v1/auth/refresh')) {
      return Promise.reject(error);
    }

    originalRequest._retried = true;

    if (isRefreshing) {
      // Queue request until refresh completes
      return new Promise<string>((resolve) => {
        refreshQueue.push(resolve);
      }).then((newToken) => {
        originalRequest.headers.Authorization = `Bearer ${newToken}`;
        return client(originalRequest);
      });
    }

    isRefreshing = true;

    try {
      const storedRefresh = await SecureStore.getItemAsync(
        SECURE_STORE_KEYS.REFRESH_TOKEN,
      );

      if (!storedRefresh) throw new Error('No refresh token');

      const { data } = await axios.post<TokenResponse>(
        `${API_BASE_URL}/v1/auth/refresh`,
        { refreshToken: storedRefresh },
      );

      await SecureStore.setItemAsync(
        SECURE_STORE_KEYS.ACCESS_TOKEN,
        data.accessToken,
      );
      await SecureStore.setItemAsync(
        SECURE_STORE_KEYS.REFRESH_TOKEN,
        data.refreshToken,
      );

      refreshQueue.forEach((resolve) => resolve(data.accessToken));
      refreshQueue = [];

      originalRequest.headers.Authorization = `Bearer ${data.accessToken}`;
      return client(originalRequest);
    } catch {
      // Refresh failed — clear tokens and force logout
      await SecureStore.deleteItemAsync(SECURE_STORE_KEYS.ACCESS_TOKEN);
      await SecureStore.deleteItemAsync(SECURE_STORE_KEYS.REFRESH_TOKEN);
      await SecureStore.deleteItemAsync(SECURE_STORE_KEYS.ACCOUNT_ID);

      refreshQueue.forEach((resolve) => resolve(''));
      refreshQueue = [];

      _onAuthFailure?.();
      return Promise.reject(error);
    } finally {
      isRefreshing = false;
    }
  },
);

export default client;
